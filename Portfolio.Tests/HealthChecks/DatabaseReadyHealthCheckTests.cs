using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Portfolio.Repositories;
using Portfolio.Web.HealthChecks;

namespace Portfolio.Tests.HealthChecks;

/// <summary>The readiness check asks the database at most once per 10 seconds (SEC-32).</summary>
/// <remarks>
/// A live context answers Healthy; a disposed one throws, so it is Unhealthy only if the database is
/// actually asked. That makes "was the database hit" observable without a mock. The cache is static, so
/// every test resets it first (tests in one class run sequentially).
/// </remarks>
public class DatabaseReadyHealthCheckTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static PortfolioDbContext LiveContext() =>
        new(new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseInMemoryDatabase($"Health_{Guid.NewGuid()}").Options);

    private static PortfolioDbContext DisposedContext()
    {
        var ctx = LiveContext();
        ctx.Dispose();
        return ctx;
    }

    private static Task<HealthCheckResult> Run(PortfolioDbContext ctx, FakeTimeProvider clock) =>
        new DatabaseReadyHealthCheck(ctx, clock).CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);

    [Fact]
    public async Task WithinTenSeconds_TheCachedResultIsReused_WithoutAskingTheDatabase()
    {
        // Arrange
        DatabaseReadyHealthCheck.ResetCache();
        var clock = new FakeTimeProvider(Start);
        using var live = LiveContext();
        Assert.Equal(HealthStatus.Healthy, (await Run(live, clock)).Status);

        // Act
        clock.Advance(TimeSpan.FromSeconds(9));
        var second = await Run(DisposedContext(), clock);

        // Assert
        Assert.Equal(HealthStatus.Healthy, second.Status);
    }

    [Fact]
    public async Task AfterTenSeconds_TheDatabaseIsAskedAgain()
    {
        // Arrange
        DatabaseReadyHealthCheck.ResetCache();
        var clock = new FakeTimeProvider(Start);
        using var live = LiveContext();
        Assert.Equal(HealthStatus.Healthy, (await Run(live, clock)).Status);

        // Act
        clock.Advance(TimeSpan.FromSeconds(10));
        var second = await Run(DisposedContext(), clock);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, second.Status);
    }

    [Fact]
    public async Task AnUnhealthyResult_IsCachedToo_ThenRecovers()
    {
        // Arrange
        DatabaseReadyHealthCheck.ResetCache();
        var clock = new FakeTimeProvider(Start);
        Assert.Equal(HealthStatus.Unhealthy, (await Run(DisposedContext(), clock)).Status);

        // Act
        clock.Advance(TimeSpan.FromSeconds(5));
        using var live = LiveContext();
        var within = await Run(live, clock);
        clock.Advance(TimeSpan.FromSeconds(6));
        var after = await Run(live, clock);

        // Assert
        Assert.Equal(HealthStatus.Unhealthy, within.Status);
        Assert.Equal(HealthStatus.Healthy, after.Status);
    }

    [Fact]
    public async Task TheFailureDescription_NeverCarriesTheConnectionString()
    {
        // Arrange
        DatabaseReadyHealthCheck.ResetCache();

        // Act
        var result = await Run(DisposedContext(), new FakeTimeProvider(Start));

        // Assert
        Assert.Equal("Database connectivity check failed.", result.Description);
    }
}
