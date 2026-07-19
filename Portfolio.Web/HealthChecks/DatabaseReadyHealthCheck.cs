using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Portfolio.Repositories;

namespace Portfolio.Web.HealthChecks
{
    /// <summary>
    /// Readiness check: reports Healthy only when the PostgreSQL database is reachable.
    /// Backs the Kubernetes readiness probe (/health/ready) so a pod is kept out of the
    /// Service until its dependencies are up. Uses only the framework's built-in health
    /// check abstractions — no extra NuGet dependency.
    /// </summary>
    public sealed class DatabaseReadyHealthCheck : IHealthCheck
    {
        private readonly PortfolioDbContext _db;

        public DatabaseReadyHealthCheck(PortfolioDbContext db)
        {
            _db = db;
        }

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context,
            CancellationToken cancellationToken = default)
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
