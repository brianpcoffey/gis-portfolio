using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Portfolio.Web.RateLimiting;

/// <summary>
/// The process-wide concurrency cap for <see cref="CpuBoundAttribute"/> endpoints. It lives in one
/// static place so every per-client chain shares the same instance.
/// </summary>
internal static class SharedCpuConcurrency
{
    /// <summary>Permits = max(2, cores); up to 16 requests queue, oldest first.</summary>
    internal static RateLimiter Create() => new NonDisposingRateLimiter(new ConcurrencyLimiter(new ConcurrencyLimiterOptions
    {
        PermitLimit           = Math.Max(2, Environment.ProcessorCount),
        QueueLimit            = 16,
        QueueProcessingOrder  = QueueProcessingOrder.OldestFirst
    }));

    internal static readonly RateLimiter Shared = Create();
}

/// <summary>
/// Delegates everything to the wrapped limiter but ignores disposal. A chained per-client limiter
/// disposes its children when the partition is evicted; without this the shared limiter would be
/// disposed with the first idle client.
/// </summary>
internal sealed class NonDisposingRateLimiter(RateLimiter inner) : RateLimiter
{
    public override RateLimiterStatistics? GetStatistics() => inner.GetStatistics();

    public override TimeSpan? IdleDuration => inner.IdleDuration;

    protected override RateLimitLease AttemptAcquireCore(int permitCount) => inner.AttemptAcquire(permitCount);

    protected override ValueTask<RateLimitLease> AcquireAsyncCore(int permitCount, CancellationToken cancellationToken) =>
        inner.AcquireAsync(permitCount, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        // Intentionally empty: the shared limiter lives for the whole process.
    }

    protected override ValueTask DisposeAsyncCore() => ValueTask.CompletedTask;
}

/// <summary>
/// Shared 429 handling: sets Retry-After (whole seconds, rounded up) from the lease when the limiter
/// supplies one, and 5 seconds for a full concurrency queue, which has no refill time to report.
/// The body stays empty, as the default rejection writes.
/// </summary>
internal static class RateLimitRejection
{
    internal const int QueueOverflowRetryAfterSeconds = 5;

    internal static ValueTask Apply(OnRejectedContext context, CancellationToken cancellationToken)
    {
        var seconds = QueueOverflowRetryAfterSeconds;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            seconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));

        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Per-client (remote IP) policy: a per-IP window first, then, for <see cref="CpuBoundAttribute"/>
/// endpoints, the shared concurrency limiter. The window comes first so an over-budget client is
/// rejected at once and never takes a place in the shared queue.
/// </summary>
internal abstract class ClientRateLimiterPolicy(RateLimiter sharedCpu) : IRateLimiterPolicy<string>
{
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected { get; } = RateLimitRejection.Apply;

    /// <summary>Builds one client's own window limiter.</summary>
    protected abstract RateLimiter CreateWindow();

    public RateLimitPartition<string> GetPartition(HttpContext httpContext)
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var cpuBound = httpContext.GetEndpoint()?.Metadata.GetMetadata<CpuBoundAttribute>() is not null;

        // Two partitions per client: a CPU-bound endpoint's chain and a plain window.
        return cpuBound
            ? RateLimitPartition.Get(ip + "|cpu", _ => RateLimiter.CreateChained(CreateWindow(), sharedCpu))
            : RateLimitPartition.Get(ip, _ => CreateWindow());
    }
}

/// <summary>"compute": a sliding 60/min budget (viewshed runs on every cell click).</summary>
internal sealed class ComputeRateLimiterPolicy(RateLimiter sharedCpu) : ClientRateLimiterPolicy(sharedCpu)
{
    public const string Name = "compute";

    protected override RateLimiter CreateWindow() => new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
    {
        PermitLimit       = 60,
        Window            = TimeSpan.FromMinutes(1),
        SegmentsPerWindow = 6,
        QueueLimit        = 0
    });
}

/// <summary>"expensive": a fixed 15/min budget for paid-API and heavy endpoints.</summary>
internal sealed class ExpensiveRateLimiterPolicy(RateLimiter sharedCpu) : ClientRateLimiterPolicy(sharedCpu)
{
    public const string Name = "expensive";

    protected override RateLimiter CreateWindow() => new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
    {
        PermitLimit = 15,
        Window      = TimeSpan.FromMinutes(1),
        QueueLimit  = 0
    });
}
