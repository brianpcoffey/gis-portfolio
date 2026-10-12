using System.Net;

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
    public async Task Expensive_Allows15_ThenReturns429()
    {
        await using var f = new PortfolioAppFactory();
        var c = f.Client();

        var codes = await Send(c, 16, _ => null);

        AssertFifteenThenLimited(codes);
        var limited = await c.GetAsync(Path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.Equal("nosniff", limited.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task Xff_CaseA_NoHeader_IsLimitedAfter15()
    {
        await using var f = new PortfolioAppFactory();
        AssertFifteenThenLimited(await Send(f.Client(), 16, _ => null));
    }

    [Fact]
    public async Task Xff_CaseB_RotatingSingleEntry_BypassesTheLimit()
    {
        await using var f = new PortfolioAppFactory();
        AssertNeverLimited(await Send(f.Client(), 16, i => $"203.0.113.{i + 1}"));
    }

    [Fact]
    public async Task Xff_CaseC_RotatingLeftEntry_FixedRight_IsLimitedAfter15()
    {
        await using var f = new PortfolioAppFactory();
        AssertFifteenThenLimited(await Send(f.Client(), 16, i => $"198.51.100.{i + 1}, 192.0.2.77"));
    }

    [Fact]
    public async Task Xff_CaseD_FixedLeft_RotatingRight_BypassesTheLimit()
    {
        await using var f = new PortfolioAppFactory();
        AssertNeverLimited(await Send(f.Client(), 16, i => $"192.0.2.88, 198.51.100.{i + 1}"));
    }
}
