using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Portfolio.Tests.Integration;

/// <summary>
/// WP-20: Report-Only CSP, nonce stamping, API no-store and the public /csp-report sink.
/// </summary>
public class CspTests
{
    private const string EnforcedCsp = "frame-ancestors 'none'; base-uri 'self'; object-src 'none'";

    private sealed class CapturingProvider : ILoggerProvider
    {
        public ConcurrentQueue<string> Lines { get; } = new();
        public ILogger CreateLogger(string categoryName) => new Capture(categoryName, Lines);
        public void Dispose() { }

        private sealed class Capture(string category, ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (category.EndsWith("CspReportController", StringComparison.Ordinal))
                    lines.Enqueue($"{logLevel}|{formatter(state, exception)}");
            }
        }
    }

    private static async Task<HttpResponseMessage> GetHtml(HttpClient c, string path)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, path);
        req.Headers.Accept.ParseAdd("text/html");
        return await c.SendAsync(req, TestContext.Current.CancellationToken);
    }

    private static Task<HttpResponseMessage> PostReport(HttpClient c, string contentType, string body)
    {
        var content = new StringContent(body, Encoding.UTF8);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        return c.PostAsync("/csp-report", content, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/Projects/Fleet")]
    public async Task Page_HasReportOnlyPolicy_AndEveryScriptAndStyleCarriesTheSameNonce(string path)
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await GetHtml(f.Client(), path);
        var html = await r.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(EnforcedCsp, r.Headers.GetValues("Content-Security-Policy").Single());
        var policy = r.Headers.GetValues("Content-Security-Policy-Report-Only").Single();
        var nonce = Regex.Match(policy, @"script-src 'self' 'nonce-([^']+)'").Groups[1].Value;
        Assert.Matches("^[0-9a-f]{32}$", nonce);

        var tags = Regex.Matches(html, @"<(script|style)\b[^>]*>", RegexOptions.IgnoreCase).Select(m => m.Value).ToList();
        Assert.NotEmpty(tags);
        Assert.All(tags, t =>
        {
            var attr = Regex.Match(t, "nonce=\"([^\"]*)\"");
            Assert.True(attr.Success, t);
            Assert.Equal(nonce, System.Net.WebUtility.HtmlDecode(attr.Groups[1].Value));
        });
    }

    [Fact]
    public async Task Nonce_DiffersBetweenRequests()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var c = f.Client();

        // Act
        var a = (await GetHtml(c, "/")).Headers.GetValues("Content-Security-Policy-Report-Only").Single();
        var b = (await GetHtml(c, "/")).Headers.GetValues("Content-Security-Policy-Report-Only").Single();

        // Assert
        Assert.NotEqual(a, b);
    }

    [Fact]
    public async Task Page_HasIsolationAndPermissionsHeaders()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await GetHtml(f.Client(), "/");

        // Assert
        Assert.Equal("csp=\"/csp-report\"", r.Headers.GetValues("Reporting-Endpoints").Single());
        Assert.Equal("same-origin", r.Headers.GetValues("Cross-Origin-Opener-Policy").Single());
        Assert.Equal("same-origin", r.Headers.GetValues("Cross-Origin-Resource-Policy").Single());
        Assert.Contains("camera=()", r.Headers.GetValues("Permissions-Policy").Single());
    }

    [Fact]
    public async Task ApiJson_WithoutCaching_GetsNoStore()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await f.Client().GetAsync("/api/v1/network/graph", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("no-store", r.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task ApiJson_WithResponseCache_KeepsItsCaching()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act
        var r = await f.Client().GetAsync("/api/v1/catrisk/book", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("public, max-age=3600", r.Headers.CacheControl?.ToString());
    }

    [Theory]
    [InlineData("application/csp-report")]
    [InlineData("application/reports+json")]
    public async Task CspReport_AcceptsBothContentTypes_Returns204_AndLogsOneSanitizedLine(string contentType)
    {
        // Arrange
        var logs = new CapturingProvider();
        await using var f = new PortfolioAppFactory();
        using var client = f.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs))).CreateClient();
        var body = contentType == "application/csp-report"
            ? """{"csp-report":{"document-uri":"https://x.test/Projects/Fleet?secret=1","effective-directive":"img-src","blocked-uri":"https://evil.test/a.png?token=abc","source-file":"https://x.test/js/a.js","line-number":7}}"""
            : """[{"type":"csp-violation","url":"https://x.test/","body":{"documentURL":"https://x.test/Projects/Fleet?secret=1","effectiveDirective":"img-src","blockedURL":"https://evil.test/a.png?token=abc","sourceFile":"https://x.test/js/a.js","lineNumber":7}}]""";

        // Act
        var r = await PostReport(client, contentType, body);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        var line = Assert.Single(logs.Lines);
        Assert.StartsWith("Warning|", line);
        Assert.Contains("directive=img-src", line);
        Assert.Contains("blocked=https://evil.test ", line);
        Assert.Contains("document=/Projects/Fleet ", line);
        Assert.DoesNotContain("secret", line);
        Assert.DoesNotContain("token", line);
    }

    [Theory]
    [InlineData("application/csp-report")]
    [InlineData("application/reports+json")]
    public async Task CspReport_ArcGisEval_IsDroppedFromTheLog(string contentType)
    {
        // Arrange
        var logs = new CapturingProvider();
        await using var f = new PortfolioAppFactory();
        using var client = f.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs))).CreateClient();
        var body = contentType == "application/csp-report"
            ? """{"csp-report":{"document-uri":"https://x.test/Projects/HomeFinder","effective-directive":"script-src","blocked-uri":"eval","source-file":"https://js.arcgis.com/4.29/","line-number":80}}"""
            : """[{"type":"csp-violation","body":{"documentURL":"https://x.test/Projects/HomeFinder","effectiveDirective":"script-src","blockedURL":"eval","sourceFile":"https://js.arcgis.com/4.29/esri/views/MapView.js","lineNumber":324}}]""";

        // Act
        var r = await PostReport(client, contentType, body);

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, r.StatusCode);
        Assert.Empty(logs.Lines);
    }

    [Fact]
    public async Task CspReport_BodyOver8Kb_Returns413()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var body = "{\"csp-report\":{\"blocked-uri\":\"" + new string('a', 9000) + "\"}}";

        // Act
        var r = await PostReport(f.Client(), "application/csp-report", body);

        // Assert
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, r.StatusCode);
    }

    [Fact]
    public async Task CspReport_OtherContentType_Returns415_AndMalformedJson_Returns204()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var c = f.Client();

        // Act
        var wrong = await PostReport(c, "text/plain", "{}");
        var malformed = await PostReport(c, "application/csp-report", "{not json");

        // Assert
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, wrong.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, malformed.StatusCode);
    }

    [Fact]
    public async Task CspReport_IsRateLimitedAt30PerMinute()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var c = f.Client();
        var codes = new List<HttpStatusCode>();

        // Act
        for (var i = 0; i < 31; i++)
            codes.Add((await PostReport(c, "application/csp-report", "{}")).StatusCode);

        // Assert
        Assert.All(codes.Take(30), s => Assert.Equal(HttpStatusCode.NoContent, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[30]);
    }
}
