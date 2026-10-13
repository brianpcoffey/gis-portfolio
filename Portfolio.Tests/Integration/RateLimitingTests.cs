using System.Net;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Portfolio.Web.Controllers.Api;
using Portfolio.Web.RateLimiting;
using System.Reflection;
using System.Threading.RateLimiting;

namespace Portfolio.Tests.Integration;

/// <summary>
/// Pins the "expensive" policy (15 requests per minute per client key) on a cheap, anonymous, DB-free
/// endpoint, and today's X-Forwarded-For partitioning semantics (findings-security 4.2).
/// </summary>
/// <remarks>
/// ForwardedHeaders is configured with the defaults (ForwardLimit 1) and no trusted-proxy list, so
/// the right-most X-Forwarded-For entry becomes the partition key. Cases (b) and (d) are a known
/// bypass; they are pinned as today's behaviour, not endorsed. One factory per test, because the
/// limiter state lives in the host.
/// </remarks>
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

    private static HttpContext ContextFor(Type? controller, string ip = "203.0.113.9")
    {
        var ctx = new DefaultHttpContext();
        ctx.Connection.RemoteIpAddress = IPAddress.Parse(ip);
        var metadata = controller is null
            ? new EndpointMetadataCollection()
            : new EndpointMetadataCollection(controller.GetCustomAttributes(inherit: true).ToArray());
        ctx.SetEndpoint(new Endpoint(_ => Task.CompletedTask, metadata, "test"));
        return ctx;
    }

    private static RateLimiter Limiter(IRateLimiterPolicy<string> policy, HttpContext ctx)
    {
        var partition = policy.GetPartition(ctx);
        return partition.Factory(partition.PartitionKey);
    }

    private static ConcurrencyLimiter TinyShared(int permits = 1, int queue = 1) =>
        new(new ConcurrencyLimiterOptions { PermitLimit = permits, QueueLimit = queue });

    [Fact]
    public async Task OverBudgetClient_IsRejectedImmediately_WhileTheSharedQueueIsBusy()
    {
        // Arrange: the client spends its 15/min while the shared limiter is free ...
        using var shared = TinyShared(1, 1);
        var policy = new ExpensiveRateLimiterPolicy(new NonDisposingRateLimiter(shared));
        var limiter = Limiter(policy, ContextFor(typeof(CatRiskController)));
        for (var i = 0; i < 15; i++) limiter.AttemptAcquire().Dispose();

        // ... then the shared limiter becomes fully busy, with its queue full.
        using var held = shared.AttemptAcquire();
        var queued = shared.AcquireAsync();
        Assert.Equal(1, shared.GetStatistics()!.CurrentQueuedCount);

        // Act
        var lease = await limiter.AcquireAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        // Assert: refused at once by the client's own window, and it did not join the queue.
        Assert.False(lease.IsAcquired);
        Assert.True(lease.TryGetMetadata(MetadataName.RetryAfter, out _));
        Assert.Equal(1, shared.GetStatistics()!.CurrentQueuedCount);
        held.Dispose();
        (await queued).Dispose();
    }

    [Fact]
    public async Task CpuBoundEndpoint_WithinBudget_QueueOverflow_IsRejected()
    {
        // Arrange
        using var shared = TinyShared(1, 1);
        using var held = shared.AttemptAcquire();
        var queued = shared.AcquireAsync();
        var limiter = Limiter(new ComputeRateLimiterPolicy(new NonDisposingRateLimiter(shared)), ContextFor(typeof(ViewshedController)));

        // Act
        var lease = await limiter.AcquireAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10));

        // Assert
        Assert.False(lease.IsAcquired);
        held.Dispose();
        (await queued).Dispose();
    }

    [Fact]
    public async Task ArcGisProxyEndpoint_IsNotThrottledByTheSharedConcurrencyLimiter()
    {
        // Arrange: shared limiter completely busy.
        using var shared = TinyShared(1, 0);
        using var held = shared.AttemptAcquire();
        var policy = new ExpensiveRateLimiterPolicy(new NonDisposingRateLimiter(shared));

        // Act
        var proxy = await Limiter(policy, ContextFor(typeof(BatchGeocodingController))).AcquireAsync();
        var cpu = await Limiter(policy, ContextFor(typeof(CatRiskController))).AcquireAsync();

        // Assert
        Assert.True(proxy.IsAcquired);
        Assert.False(cpu.IsAcquired);
    }

    [Fact]
    public async Task Partition_Eviction_DoesNotDisposeTheSharedLimiter()
    {
        // Arrange
        using var shared = TinyShared(1, 0);
        var policy = new ComputeRateLimiterPolicy(new NonDisposingRateLimiter(shared));
        var limiter = Limiter(policy, ContextFor(typeof(ViewshedController)));

        // Act
        limiter.Dispose();
        await limiter.DisposeAsync();

        // Assert: still usable.
        using var lease = shared.AttemptAcquire();
        Assert.True(lease.IsAcquired);
    }

    [Fact]
    public async Task QueuedRequest_ConsumesExactlyOneGlobalPermit()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var options = f.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value;
        var ctx = ContextFor(typeof(ViewshedController), "198.51.100.200");
        var before = options.GlobalLimiter!.GetStatistics(ctx)!.CurrentAvailablePermits;

        // Act: the global limiter is one fixed window (no concurrency in the chain).
        using var lease = await options.GlobalLimiter.AcquireAsync(ctx);

        // Assert
        Assert.True(lease.IsAcquired);
        Assert.Equal(300, before);
        Assert.Equal(299, options.GlobalLimiter.GetStatistics(ctx)!.CurrentAvailablePermits);
    }

    [Fact]
    public async Task GlobalLimiter_IsNotBlockedByTheSharedConcurrencyLimiter()
    {
        // Arrange: saturate the real shared limiter.
        await using var f = new PortfolioAppFactory();
        var options = f.Services.GetRequiredService<IOptions<RateLimiterOptions>>().Value;
        var leases = new List<RateLimitLease>();
        for (var i = 0; i < Math.Max(2, Environment.ProcessorCount); i++)
            leases.Add(SharedCpuConcurrency.Shared.AttemptAcquire());
        try
        {
            // Act
            using var lease = await options.GlobalLimiter!.AcquireAsync(ContextFor(typeof(ViewshedController), "198.51.100.201"))
                .AsTask().WaitAsync(TimeSpan.FromSeconds(10));

            // Assert
            Assert.True(lease.IsAcquired);
        }
        finally
        {
            foreach (var l in leases) l.Dispose();
        }
    }

    [Fact]
    public void CpuBound_IsOnEveryCpuBoundController_AndNoArcGisProxy()
    {
        // Arrange
        Type[] cpu =
        [
            typeof(GeoStreamController), typeof(SpatialGeometryController), typeof(RasterTerrainController),
            typeof(SpatialNetworkController), typeof(SpatialClusterController), typeof(ViewshedController),
            typeof(SpatialOverlayController), typeof(CatRiskController), typeof(ChangeDetectionController),
            typeof(FleetRoutingController), typeof(OutageController), typeof(ResponseCoverageController),
            typeof(HomeFinderController)
        ];
        Type[] proxies =
        [
            typeof(BatchGeocodingController), typeof(FeaturesController),
            typeof(ReverseGeocodingController), typeof(AddressStandardizationController)
        ];

        // Act & Assert
        Assert.All(cpu, t => Assert.NotNull(t.GetCustomAttribute<CpuBoundAttribute>()));
        Assert.All(proxies, t => Assert.Null(t.GetCustomAttribute<CpuBoundAttribute>()));
    }

    [Fact]
    public async Task Rejection_CarriesRetryAfter_ForWindowAndForQueueOverflow()
    {
        // Arrange
        await using var f = new PortfolioAppFactory();
        var c = f.Client();
        await Send(c, 15, _ => null);

        // Act
        var windowLimited = await c.GetAsync(Path, TestContext.Current.CancellationToken);

        // Assert: seconds, rounded up, within the 60 s window.
        Assert.Equal(HttpStatusCode.TooManyRequests, windowLimited.StatusCode);
        var seconds = int.Parse(windowLimited.Headers.GetValues("Retry-After").Single());
        Assert.InRange(seconds, 1, 60);

        // Queue overflow: the lease has no RetryAfter metadata, so 5.
        using var shared = TinyShared(1, 0);
        using var held = shared.AttemptAcquire();
        using var overflow = await shared.AcquireAsync();
        var ctx = new DefaultHttpContext();
        await RateLimitRejection.Apply(new OnRejectedContext { HttpContext = ctx, Lease = overflow }, CancellationToken.None);
        Assert.Equal("5", ctx.Response.Headers.RetryAfter.ToString());
    }
}
