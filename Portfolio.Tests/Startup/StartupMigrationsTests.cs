using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
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

    // Nothing listens on port 1, so the first statement against it fails fast; no real database is involved.
    // Placeholder values only, built from properties so no connection-string literal sits in the source.
    private static readonly string UnreachablePostgres = new NpgsqlConnectionStringBuilder
    {
        Host = "127.0.0.1",
        Port = 1,
        Database = "x",
        Username = "x",
        Password = "x",
        Timeout = 1,
    }.ConnectionString;

    private static WebApplication AppWithUnreachablePostgres()
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddDbContext<PortfolioDbContext>(o => o.UseNpgsql(UnreachablePostgres));
        return builder.Build();
    }

    // CapturingLogger keeps the formatted text only; these tests also need the exception that was logged.
    private sealed class ExceptionCapturingLogger : ILogger
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception), exception));
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

    // The InMemory provider skips the dedupe pre-step (it is not relational), so these two use Npgsql against a server
    // that is not there: the pending-migrations lookup the pre-step starts with is then the first thing to fail, and it
    // must land in the same catch as a Migrate() failure.
    [Fact]
    public void Run_WithTheMigrateFlagAndAnUnreachableDatabase_LogsTheFailureAndExitsWithOne()
    {
        // Arrange
        using var app = AppWithUnreachablePostgres();
        var logger = new ExceptionCapturingLogger();

        // Act
        var exitCode = StartupMigrations.Run(app, ["--migrate"], Config(null), logger);

        // Assert
        Assert.Equal(1, exitCode);
        var failure = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.StartsWith("Database migration failed.", failure.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<NpgsqlException>(failure.Exception);
        Assert.Contains("GetPendingMigrations", failure.Exception!.StackTrace, StringComparison.Ordinal);
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("Renumbered", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("Database migration successful.", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("Migration-only run complete", StringComparison.Ordinal));
    }

    [Fact]
    public void Run_AtStartupWithAnUnreachableDatabase_LogsTheFailureAndContinuesToTheHost()
    {
        // Arrange
        using var app = AppWithUnreachablePostgres();
        var logger = new ExceptionCapturingLogger();

        // Act
        var exitCode = StartupMigrations.Run(app, [], Config(null), logger);

        // Assert: tolerated, as for any other startup migration failure.
        Assert.Null(exitCode);
        var failure = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.StartsWith("Database migration failed.", failure.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<NpgsqlException>(failure.Exception);
        Assert.Contains("GetPendingMigrations", failure.Exception!.StackTrace, StringComparison.Ordinal);
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("Database migration successful.", StringComparison.Ordinal));
    }
}
