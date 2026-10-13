using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Portfolio.Repositories;
using Portfolio.Tests.Repositories;
using Portfolio.Web.Startup;

namespace Portfolio.Tests.Startup;

/// <summary>
/// The decision and the failure handling around the pre-migration order-number dedupe. The SQL itself needs a real
/// Postgres (the EF InMemory provider runs no SQL), so it is rehearsed against one and not unit-tested here.
/// </summary>
public class StartupMigrationsTests
{
    // The migration ids come from the real model; no connection is opened to list them.
    private static List<string> AllMigrationIds()
    {
        using var db = new PortfolioDbContext(new DbContextOptionsBuilder<PortfolioDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .Options);
        return db.Database.GetMigrations().ToList();
    }

    private static WebApplication AppWithInMemoryDatabase()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddDbContext<PortfolioDbContext>(o => o.UseInMemoryDatabase($"StartupMigrations_{Guid.NewGuid()}"));
        return builder.Build();
    }

    private static IConfiguration Config(string? runMigrationsOnStartup) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(runMigrationsOnStartup is null
                ? []
                : [new KeyValuePair<string, string?>("RunMigrationsOnStartup", runMigrationsOnStartup)])
            .Build();

    [Fact]
    public void ShouldDedupe_WhenTheUniqueIndexMigrationIsPendingOnAnExistingDatabase_ReturnsTrue()
    {
        // Arrange: a database that already ran InitialCreate, so everything after it is pending.
        var pending = AllMigrationIds().Where(id => !id.EndsWith("_InitialCreate", StringComparison.Ordinal)).ToList();

        // Act
        var dedupe = StartupMigrations.ShouldDedupe(pending);

        // Assert
        Assert.Contains(pending, id => id.EndsWith("_UniqueOrderNumberPerUser", StringComparison.Ordinal));
        Assert.True(dedupe);
    }

    [Fact]
    public void ShouldDedupe_WhenOnlyTheUniqueIndexMigrationIsPending_ReturnsTrue()
    {
        // Arrange
        var pending = AllMigrationIds().Where(id => id.EndsWith("_UniqueOrderNumberPerUser", StringComparison.Ordinal)).ToList();

        // Act
        var dedupe = StartupMigrations.ShouldDedupe(pending);

        // Assert
        Assert.Single(pending);
        Assert.True(dedupe);
    }

    [Fact]
    public void ShouldDedupe_OnAFreshDatabaseWhereInitialCreateIsPendingToo_ReturnsFalse()
    {
        // Arrange: nothing is applied yet, so the FiberOrders table does not exist.
        var pending = AllMigrationIds();

        // Act
        var dedupe = StartupMigrations.ShouldDedupe(pending);

        // Assert
        Assert.Contains(pending, id => id.EndsWith("_InitialCreate", StringComparison.Ordinal));
        Assert.Contains(pending, id => id.EndsWith("_UniqueOrderNumberPerUser", StringComparison.Ordinal));
        Assert.False(dedupe);
    }

    [Fact]
    public void ShouldDedupe_WhenNothingIsPending_ReturnsFalse()
    {
        // Arrange & Act
        var dedupe = StartupMigrations.ShouldDedupe([]);

        // Assert
        Assert.False(dedupe);
    }

    [Fact]
    public void ShouldDedupe_WhenAnEarlierMigrationIsPendingButNotTheUniqueIndexOne_ReturnsFalse()
    {
        // Arrange
        var pending = AllMigrationIds().Where(id => id.EndsWith("_ExpandRedlandsProperties", StringComparison.Ordinal)).ToList();

        // Act
        var dedupe = StartupMigrations.ShouldDedupe(pending);

        // Assert
        Assert.Single(pending);
        Assert.False(dedupe);
    }

    [Theory]
    [InlineData("20261013192510_UniqueOrderNumberPerUser", true)]
    [InlineData("20261013192510_NotUniqueOrderNumberPerUser", false)]
    [InlineData("UniqueOrderNumberPerUser", false)]
    [InlineData("20261013192510_uniqueordernumberperuser", false)]
    public void ShouldDedupe_MatchesTheMigrationNameExactly(string migrationId, bool expected)
    {
        // Arrange & Act
        var dedupe = StartupMigrations.ShouldDedupe([migrationId]);

        // Assert
        Assert.Equal(expected, dedupe);
    }

    [Fact]
    public void MigrationSuffixes_MatchExactlyOneRealMigrationEach()
    {
        // Arrange
        var ids = AllMigrationIds();

        // Act & Assert: a renamed migration would silently turn the dedupe off.
        Assert.Single(ids, id => id.EndsWith(StartupMigrations.UniqueOrderNumberMigrationSuffix, StringComparison.Ordinal));
        Assert.Single(ids, id => id.EndsWith(StartupMigrations.InitialCreateMigrationSuffix, StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WithMigrationsDisabledAndNoFlag_ReturnsNullWithoutTouchingTheDatabase()
    {
        // Arrange
        using var app = AppWithInMemoryDatabase();
        var logger = new CapturingLogger<object>();

        // Act
        var exitCode = StartupMigrations.Run(app, [], Config("false"), logger);

        // Assert
        Assert.Null(exitCode);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public void Run_WithTheMigrateFlagOnANonRelationalProvider_LogsTheFailureAndExitsWithOne()
    {
        // Arrange: Migrate() cannot run on the InMemory provider, which stands in for any migration failure.
        using var app = AppWithInMemoryDatabase();
        var logger = new CapturingLogger<object>();

        // Act
        var exitCode = StartupMigrations.Run(app, ["--migrate"], Config(null), logger);

        // Assert
        Assert.Equal(1, exitCode);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.StartsWith("Database migration failed.", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("Renumbered", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("Database migration successful.", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_WithAStartupMigrationFailure_ToleratesItAndContinuesToTheHost()
    {
        // Arrange
        using var app = AppWithInMemoryDatabase();
        var logger = new CapturingLogger<object>();

        // Act
        var exitCode = StartupMigrations.Run(app, [], Config(null), logger);

        // Assert
        Assert.Null(exitCode);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Error && e.Message.StartsWith("Database migration failed.", StringComparison.Ordinal));
    }
}
