using System.Net;

namespace Portfolio.Tests.Integration;

/// <summary>
/// Static files: versioned URLs (?v=) are immutable for a year, unversioned ones cache for an hour,
/// and ETag / 304 and the security headers keep working.
/// </summary>
public class StaticCachingTests
{
    [Fact]
    public async Task VersionedAsset_IsCachedImmutablyForAYear()
    {
        await using var f = new PortfolioAppFactory();

        var r = await f.Client().GetAsync("/css/base.css?v=abc", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("public,max-age=31536000,immutable", string.Join(";", r.Headers.GetValues("Cache-Control")).Replace(" ", "", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnversionedAsset_IsCachedForAnHour()
    {
        await using var f = new PortfolioAppFactory();

        var r = await f.Client().GetAsync("/css/base.css", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("public,max-age=3600", string.Join(";", r.Headers.GetValues("Cache-Control")).Replace(" ", "", StringComparison.Ordinal));
    }

    [Fact]
    public async Task StaticAsset_SupportsETagAndConditional304_AndKeepsSecurityHeaders()
    {
        await using var f = new PortfolioAppFactory();
        var client = f.Client();

        var first = await client.GetAsync("/css/base.css", TestContext.Current.CancellationToken);
        var etag = first.Headers.ETag;
        Assert.NotNull(etag);
        Assert.True(first.Headers.Contains("X-Content-Type-Options"));

        var req = new HttpRequestMessage(HttpMethod.Get, "/css/base.css");
        req.Headers.IfNoneMatch.Add(etag);
        var second = await client.SendAsync(req, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task HtmlPage_DoesNotGetTheLongStaticCache()
    {
        await using var f = new PortfolioAppFactory();
        var req = new HttpRequestMessage(HttpMethod.Get, "/Privacy");
        req.Headers.Accept.ParseAdd("text/html");

        var r = await f.Client().SendAsync(req, TestContext.Current.CancellationToken);

        Assert.DoesNotContain("immutable", string.Join(";", r.Headers.TryGetValues("Cache-Control", out var v) ? v : []), StringComparison.Ordinal);
    }
}
