using Microsoft.Extensions.Diagnostics.HealthChecks;
using Portfolio.Repositories;

namespace Portfolio.Web.HealthChecks
{
    /// <summary>
    /// Readiness check behind the Kubernetes probe at /health/ready: reports Healthy only when the
    /// PostgreSQL database is reachable.
    /// </summary>
    /// <remarks>
    /// /health/ready is unthrottled, and on a serverless database every probe can wake the
    /// compute, so the result (healthy or not) is cached process-wide for
    /// <see cref="CacheDuration"/>. A recovered database is therefore noticed within that window.
    /// </remarks>
    public sealed class DatabaseReadyHealthCheck : IHealthCheck
    {
        /// <summary>How long a probe result is reused before the database is asked again.</summary>
        internal static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(10);

        // Static: the check is resolved per probe, so the cache has to outlive the instance.
        private static readonly SemaphoreSlim Gate = new(1, 1);
        private sealed record Entry(HealthCheckResult Result, DateTimeOffset ExpiresAt);
        private static Entry? _cached;

        private readonly PortfolioDbContext _db;
        private readonly TimeProvider _time;

        public DatabaseReadyHealthCheck(PortfolioDbContext db, TimeProvider time)
        {
            _db = db;
            _time = time;
        }

        /// <summary>Drops the cached result (for tests, which share the static cache).</summary>
        internal static void ResetCache() => Volatile.Write(ref _cached, null);

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
        {
            if (TryGetFresh(out var cached))
                return cached;

            // One probe at a time: concurrent callers wait and then reuse the winner's result
            // instead of each hitting the database.
            await Gate.WaitAsync(cancellationToken);
            try
            {
                if (TryGetFresh(out cached))
                    return cached;

                var result = await QueryAsync(cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                    Volatile.Write(ref _cached, new Entry(result, _time.GetUtcNow() + CacheDuration));
                return result;
            }
            finally
            {
                Gate.Release();
            }
        }

        private bool TryGetFresh(out HealthCheckResult result)
        {
            var snapshot = Volatile.Read(ref _cached);
            if (snapshot is { } c && _time.GetUtcNow() < c.ExpiresAt)
            {
                result = c.Result;
                return true;
            }

            result = default;
            return false;
        }

        private async Task<HealthCheckResult> QueryAsync(CancellationToken cancellationToken)
        {
            try
            {
                var canConnect = await _db.Database.CanConnectAsync(cancellationToken);
                return canConnect
                    ? HealthCheckResult.Healthy("Database reachable.")
                    : HealthCheckResult.Unhealthy("Database not reachable.");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Unhealthy("Database connectivity check failed.", ex);
            }
        }
    }
}
