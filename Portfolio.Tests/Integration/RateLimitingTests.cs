using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;

namespace Portfolio.Tests.Integration;

/// <summary>
/// Pins the "expensive" policy (15 requests per minute per client key) on a cheap, anonymous,
/// DB-free endpoint, and today's X-Forwarded-For partitioning semantics (findings-security 4.2).
/// ForwardedHeaders is configured with the defaults (ForwardLimit 1) and no trusted-proxy list, so
/// the right-most X-Forwarded-For entry becomes the partition key. Cases (b) and (d) are a known
/// bypass; they are pinned as today's behaviour, not endorsed. One factory per test, because the
/// limiter state lives in the host.
/// </summary>
public class RateLimitingTests
{
    private const string Path = "/api/v1/catrisk/book";

    private static async Task<List<HttpStatusCode>> Send(HttpClient c, int count, Func<int, string?> xff)
    {
        var results = new List<HttpStatusCode>();
        for (var i = 0; i < count; i++)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, Path);
            var value = xff(i);
            if (value is not null) req.Headers.TryAddWithoutValidation("X-Forwarded-For", value);
            var r = await c.SendAsync(req, TestContext.Current.CancellationToken);
            results.Add(r.StatusCode);
        }
        return results;
    }

    private static void AssertFifteenThenLimited(List<HttpStatusCode> codes)
    {
        Assert.Equal(16, codes.Count);
        Assert.All(codes.Take(15), s => Assert.Equal(HttpStatusCode.OK, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[15]);
    }

    private static void AssertNeverLimited(List<HttpStatusCode> codes)
    {
        Assert.Equal(16, codes.Count);
        Assert.All(codes, s => Assert.Equal(HttpStatusCode.OK, s));
    }

    [Fact]
    public async Task Expensive_Allows15_ThenReturns429() // also XFF case (a): no header
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var c = f.Client();

        // Act
        var codes = await Send(c, 16, _ => null);

        // Assert
        AssertFifteenThenLimited(codes);
        var limited = await c.GetAsync(Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("nosniff", limited.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Xff_CaseB_RotatingSingleEntry_BypassesTheLimit()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act & Assert
        AssertNeverLimited(await Send(f.Client(), 16, i => $"203.0.113.{i + 1}"));
    }

    [Fact]
    public async Task Xff_CaseC_RotatingLeftEntry_FixedRight_IsLimitedAfter15()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act & Assert
        AssertFifteenThenLimited(await Send(f.Client(), 16, i => $"198.51.100.{i + 1}, 192.0.2.77"));
    }

    [Fact]
    public async Task Xff_CaseD_FixedLeft_RotatingRight_BypassesTheLimit()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();

        // Act & Assert
        AssertNeverLimited(await Send(f.Client(), 16, i => $"192.0.2.88, 198.51.100.{i + 1}"));
    }

    [Fact]
    public async Task Compute_Allows60_ThenReturns429_OnThe61st()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var c = f.Client();
        var codes = new List<HttpStatusCode>();

        // Act
        for (var i = 0; i < 61; i++)
        {
            using var body = new StringContent("{}", Encoding.UTF8, "application/json");
            var r = await c.PostAsync("/api/v1/geometry/triangulate", body, TestContext.Current.CancellationToken);
            codes.Add(r.StatusCode);
        }

        // Assert
        // The tiny body is rejected by validation (400); only the limiter's verdict matters here.
        Assert.All(codes.Take(60), s => Assert.NotEqual(HttpStatusCode.TooManyRequests, s));
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[60]);
    }

    [Fact]
    public async Task Compute_SharesOneBudgetAcrossComputeControllers()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var c = f.Client();
        string[] paths = ["/api/v1/geometry/triangulate", "/api/v1/clustering/dbscan", "/api/v1/viewshed/compute"];
        var codes = new List<HttpStatusCode>();

        // Act
        for (var i = 0; i < 61; i++)
        {
            using var body = new StringContent("{}", Encoding.UTF8, "application/json");
            codes.Add((await c.PostAsync(paths[i % paths.Length], body, TestContext.Current.CancellationToken)).StatusCode);
        }

        // Assert
        Assert.DoesNotContain(HttpStatusCode.TooManyRequests, codes.Take(60));
        Assert.Equal(HttpStatusCode.TooManyRequests, codes[60]);
    }

    private static HttpContext ContextWithPolicy(string? policy)
    {
        var ctx = new DefaultHttpContext();
        var metadata = policy is null ? new EndpointMetadataCollection() : new EndpointMetadataCollection(new EnableRateLimitingAttribute(policy));
        ctx.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test"));
        return ctx;
    }

    [Theory]
    [InlineData("compute")]
    [InlineData("expensive")]
    public void ConcurrencyKey_ComputeAndExpensive_ShareOnePartition(string policy)
    {
        // Act & Assert
        Assert.Equal(RateLimitKeys.ComputeConcurrencyKey, RateLimitKeys.ConcurrencyKey(ContextWithPolicy(policy)));
    }

    [Theory]
    [InlineData("csp")]
    [InlineData(null)]
    public void ConcurrencyKey_OtherEndpoints_AreNotLimited(string? policy)
    {
        // Act & Assert
        Assert.Null(RateLimitKeys.ConcurrencyKey(ContextWithPolicy(policy)));
    }

    [Fact]
    public void ConcurrencyKey_NoEndpoint_IsNotLimited()
    {
        // Act & Assert
        Assert.Null(RateLimitKeys.ConcurrencyKey(new DefaultHttpContext()));
    }
}
