using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Repositories.Interfaces;
using Portfolio.Repositories.Mappings;
using Portfolio.Repositories.Repositories;
using Portfolio.Repositories.Seed;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Portfolio.Tests.Repositories;
using Portfolio.Web.Startup;

namespace Portfolio.Tests.Integration;

/// <summary>
/// The migrations, the pre-migration order-number dedupe and the order-number retry against a real PostgreSQL server.
/// The EF InMemory provider runs no SQL and enforces no index, so none of this can be seen anywhere else.
/// </summary>
/// <remarks>
/// Every test skips unless <c>PORTFOLIO_PG_TEST</c> holds an Npgsql connection string for a throwaway server (CI's
/// <c>pg-migrations</c> job points it at a service container; locally any server you can create databases on works).
/// A test never uses the database named in that string for its own work: it creates a database called
/// <c>portfolio_pgtest_&lt;guid&gt;</c> through that connection, works only in the new database, and drops it afterwards,
/// so pointing the variable at a server that also hosts real data leaves that data alone.
/// <para>
/// Point the variable at a local server rather than a hosted one such as Neon: every test is a create and a drop
/// over the connection. The drop uses <c>DROP DATABASE name WITH (FORCE)</c>, which needs PostgreSQL 13 or later.
/// A run that is killed or cancelled between the create and the drop leaves its <c>portfolio_pgtest_*</c> database
/// behind. List the strays with
/// <c>SELECT datname FROM pg_database WHERE datname LIKE 'portfolio\_pgtest\_%'</c> and drop each with
/// <c>DROP DATABASE "name" WITH (FORCE)</c>.
/// </para>
/// </remarks>
public class PostgresMigrationTests
{
    private const string ConnectionStringVariable = "PORTFOLIO_PG_TEST";
    private const string TakenNumber = "ORD-20261013-0001";

    [Fact]
    public async Task Migrate_OnAnEmptyDatabase_AppliesEveryMigration()
    {
        // Arrange
        var server = RequireServer();
        await using var scratch = await ScratchDatabase.CreateAsync(server);
        await using var db = NewDb(scratch.ConnectionString);

        // Act
        await db.Database.MigrateAsync();

        // Assert
        var expected = db.Database.GetMigrations().ToList();
        Assert.Equal(expected, (await db.Database.GetAppliedMigrationsAsync()).ToList());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(RedlandsPropertySeedData.All.Count, await ScalarAsync<long>(scratch.ConnectionString, "SELECT count(*) FROM \"Properties\""));
        Assert.NotNull(await OrderNumberIndexDefinitionAsync(scratch.ConnectionString));
    }

    [Fact]
    public async Task StartupMigrations_OnAnEmptyDatabase_MigratesWithoutRenumbering()
    {
        // Arrange: the FiberOrders table does not exist yet, so a dedupe UPDATE here would fail the whole migration.
        var server = RequireServer();
        await using var scratch = await ScratchDatabase.CreateAsync(server);
        using var app = AppWithPostgres(scratch.ConnectionString);
        var logger = new CapturingLogger<object>();

        // Act
        var exitCode = StartupMigrations.Run(app, ["--migrate"], new ConfigurationBuilder().Build(), logger);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains(logger.Entries, e => e.Message.StartsWith("Database migration successful.", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("Renumbered", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Error);
        Assert.NotNull(await OrderNumberIndexDefinitionAsync(scratch.ConnectionString));
    }

    [Fact]
    public async Task Migrate_WithDuplicateOrderNumbersAndNoDedupe_FailsCreatingTheUniqueIndex()
    {
        // Arrange: the state a database can be in when UniqueOrderNumberPerUser is pending, with the race it fixes already lost once.
        var server = RequireServer();
        await using var scratch = await ScratchDatabase.CreateAsync(server);
        var previous = await MigrateToJustBeforeTheUniqueIndexAsync(scratch.ConnectionString);
        var user = Guid.NewGuid();
        await InsertOrderAsync(scratch.ConnectionString, user, TakenNumber);
        await InsertOrderAsync(scratch.ConnectionString, user, TakenNumber);
        await using var db = NewDb(scratch.ConnectionString);

        // Act
        var failure = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());

        // Assert: this is the failure the dedupe step exists to prevent, and it leaves the database where it was.
        Assert.Equal(PostgresErrorCodes.UniqueViolation, failure.SqlState);
        Assert.Contains(FiberOrderMapping.OrderNumberIndexName, failure.Message, StringComparison.Ordinal);
        Assert.Equal(previous, (await db.Database.GetAppliedMigrationsAsync()).Last());
        Assert.Null(await OrderNumberIndexDefinitionAsync(scratch.ConnectionString));
    }

    [Fact]
    public async Task StartupMigrations_WithDuplicateOrderNumbersPending_RenumbersThemThenCreatesTheUniqueIndex()
    {
        // Arrange: three rows share one user's number, and the neighbours the dedupe must leave alone sit around them.
        var server = RequireServer();
        await using var scratch = await ScratchDatabase.CreateAsync(server);
        await MigrateToJustBeforeTheUniqueIndexAsync(scratch.ConnectionString);
        var user = Guid.NewGuid();
        var keptId = await InsertOrderAsync(scratch.ConnectionString, user, TakenNumber);
        var secondId = await InsertOrderAsync(scratch.ConnectionString, user, TakenNumber);
        var thirdId = await InsertOrderAsync(scratch.ConnectionString, user, TakenNumber);
        var otherNumberId = await InsertOrderAsync(scratch.ConnectionString, user, "ORD-20261013-0002");
        var blankId = await InsertOrderAsync(scratch.ConnectionString, user, string.Empty);
        var secondBlankId = await InsertOrderAsync(scratch.ConnectionString, user, string.Empty);
        var otherUserId = await InsertOrderAsync(scratch.ConnectionString, Guid.NewGuid(), TakenNumber);
        using var app = AppWithPostgres(scratch.ConnectionString);
        var logger = new CapturingLogger<object>();

        // Act
        var exitCode = StartupMigrations.Run(app, ["--migrate"], new ConfigurationBuilder().Build(), logger);

        // Assert
        Assert.Equal(0, exitCode);
        Assert.Contains(logger.Entries, e => e.Message.StartsWith("Renumbered 2 duplicate order numbers before migrating.", StringComparison.Ordinal));
        Assert.Contains(logger.Entries, e => e.Message.StartsWith("Database migration successful.", StringComparison.Ordinal));
        Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Error);

        var numbers = await ReadOrderNumbersAsync(scratch.ConnectionString);
        Assert.Equal(TakenNumber, numbers[keptId]);
        Assert.Equal($"{TakenNumber}-{secondId}", numbers[secondId]);
        Assert.Equal($"{TakenNumber}-{thirdId}", numbers[thirdId]);
        Assert.Equal("ORD-20261013-0002", numbers[otherNumberId]);
        Assert.Equal(string.Empty, numbers[blankId]);
        Assert.Equal(string.Empty, numbers[secondBlankId]);
        Assert.Equal(TakenNumber, numbers[otherUserId]);

        var definition = await OrderNumberIndexDefinitionAsync(scratch.ConnectionString);
        Assert.NotNull(definition);
        Assert.Contains("CREATE UNIQUE INDEX", definition, StringComparison.Ordinal);
        Assert.Contains("(user_id, order_number)", definition, StringComparison.Ordinal);
        Assert.Contains("WHERE", definition, StringComparison.Ordinal);
        Assert.Contains("order_number <> ''", definition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UniqueIndex_OnAMigratedDatabase_RejectsADuplicateByIndexNameAndExemptsBlankNumbers()
    {
        // Arrange
        var server = RequireServer();
        await using var scratch = await MigratedDatabaseAsync(server);
        var user = Guid.NewGuid();
        await InsertOrderAsync(scratch.ConnectionString, user, TakenNumber);

        // Act
        var duplicate = await Assert.ThrowsAsync<PostgresException>(() => InsertOrderAsync(scratch.ConnectionString, user, TakenNumber));

        // Assert: the repository tells an order-number clash from other failures by exactly this SqlState and name.
        Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
        Assert.Equal(FiberOrderMapping.OrderNumberIndexName, duplicate.ConstraintName);

        // Assert: another user may hold the same number, and blank numbers are exempt.
        await InsertOrderAsync(scratch.ConnectionString, Guid.NewGuid(), TakenNumber);
        await InsertOrderAsync(scratch.ConnectionString, user, string.Empty);
        await InsertOrderAsync(scratch.ConnectionString, user, string.Empty);
        Assert.Equal(4, (await ReadOrderNumbersAsync(scratch.ConnectionString)).Count);
    }

    [Fact]
    public async Task TryAddAsync_WithATakenOrderNumber_ReturnsNullAndLeavesNothingTracked()
    {
        // Arrange
        var server = RequireServer();
        await using var scratch = await MigratedDatabaseAsync(server);
        var user = Guid.NewGuid();
        await InsertOrderAsync(scratch.ConnectionString, user, TakenNumber);
        await using var db = NewDb(scratch.ConnectionString);
        var repository = new FiberOrderRepository(db);

        // Act
        var rejected = await repository.TryAddAsync(NewOrder(user, TakenNumber));
        var accepted = await repository.TryAddAsync(NewOrder(user, "ORD-20261013-0002"));

        // Assert
        Assert.Null(rejected);
        Assert.NotNull(accepted);
        Assert.Equal("ORD-20261013-0002", accepted.OrderNumber);
        Assert.Single(db.ChangeTracker.Entries<FiberOrder>());
        Assert.Equal(2, (await ReadOrderNumbersAsync(scratch.ConnectionString)).Count);
    }

    [Fact]
    public async Task CreateAsync_WithTwoConcurrentCallsForOneUser_BothSucceedWithDistinctNumbers()
    {
        // Arrange: each caller reads the issued numbers, then waits for the other, so both are offered the same number.
        var server = RequireServer();
        await using var scratch = await MigratedDatabaseAsync(server);
        var user = Guid.NewGuid();
        var users = new Mock<IUserProfileService>();
        users.Setup(u => u.GetCurrentUserId()).Returns(user);
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 13, 12, 0, 0, TimeSpan.Zero));
        using var barrier = new Barrier(2);
        var inserts = new InsertCounter();

        async Task<FiberOrderDto> CreateOneAsync()
        {
            try
            {
                await using var db = NewDb(scratch.ConnectionString);
                var repository = new RendezvousRepository(new FiberOrderRepository(db), barrier, inserts);
                return await new FiberOrderService(repository, users.Object, clock).CreateAsync(NewOrderDto());
            }
            catch
            {
                // A caller that fails before the rendezvous must not leave the other waiting out the timeout, which
                // would end the test with a TimeoutException in place of the real error.
                barrier.RemoveParticipant();
                throw;
            }
        }

        // Act
        var created = await AwaitAllReportingEveryFailureAsync(Task.Run(CreateOneAsync), Task.Run(CreateOneAsync));

        // Assert: one insert lost to the unique index and succeeded on its retry with the next number.
        Assert.Equal(["ORD-20261013-0001", "ORD-20261013-0002"], created.Select(o => o.OrderNumber).Order(StringComparer.Ordinal).ToList());
        Assert.Equal(3, inserts.Attempts);
        Assert.Equal(1, inserts.Rejected);
        Assert.Equal(
            ["ORD-20261013-0001", "ORD-20261013-0002"],
            (await ReadOrderNumbersAsync(scratch.ConnectionString)).Values.Order(StringComparer.Ordinal).ToList());
    }

    // ---- gating ----

    private static string RequireServer()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
        Assert.SkipWhen(
            string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionStringVariable} to a throwaway PostgreSQL server (an Npgsql connection string) to run the real-database tests.");
        return connectionString!;
    }

    // ---- setup ----

    private static PortfolioDbContext NewDb(string connectionString) =>
        new(new DbContextOptionsBuilder<PortfolioDbContext>().UseNpgsql(connectionString).Options);

    private static WebApplication AppWithPostgres(string connectionString)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.Services.AddDbContext<PortfolioDbContext>(o => o.UseNpgsql(connectionString));
        return builder.Build();
    }

    private static async Task<ScratchDatabase> MigratedDatabaseAsync(string server)
    {
        var scratch = await ScratchDatabase.CreateAsync(server);
        try
        {
            await using var db = NewDb(scratch.ConnectionString);
            await db.Database.MigrateAsync();
            return scratch;
        }
        catch
        {
            await scratch.DisposeAsync();
            throw;
        }
    }

    // Applies every migration before UniqueOrderNumberPerUser (the state of a database that has not had this change yet)
    // and returns the id of the last one applied.
    private static async Task<string> MigrateToJustBeforeTheUniqueIndexAsync(string connectionString)
    {
        await using var db = NewDb(connectionString);
        var ids = db.Database.GetMigrations().ToList();
        var uniqueIndex = ids.FindIndex(id => id.EndsWith(StartupMigrations.UniqueOrderNumberMigrationSuffix, StringComparison.Ordinal));
        Assert.True(uniqueIndex > 0, "UniqueOrderNumberPerUser must be a migration with an earlier one before it.");
        await db.GetService<IMigrator>().MigrateAsync(ids[uniqueIndex - 1]);
        return ids[uniqueIndex - 1];
    }

    private static FiberOrder NewOrder(Guid userId, string orderNumber) => new()
    {
        UserId = userId,
        OrderNumber = orderNumber,
        ClientName = "Acme",
        ProductName = "Cable",
        Quantity = 1,
        UnitPrice = 1m,
        Status = "Pending",
        OrderDate = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc),
        ShipDate = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc)
    };

    private static CreateFiberOrderDto NewOrderDto() => new()
    {
        ClientName = "Acme",
        ProductName = "Cable",
        Quantity = 1,
        UnitPrice = 1m,
        Status = "Pending",
        OrderDate = new DateTime(2026, 10, 13, 12, 0, 0, DateTimeKind.Utc),
        ShipDate = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc)
    };

    // ---- raw SQL (the EF model is the thing under test, so the tests read and write around it) ----

    private static async Task<T?> ScalarAsync<T>(string connectionString, string sql, params NpgsqlParameter[] parameters)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddRange(parameters);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)value;
    }

    private static Task<int> InsertOrderAsync(string connectionString, Guid userId, string orderNumber) =>
        ScalarAsync<int>(
            connectionString,
            """
            INSERT INTO "FiberOrders" (user_id, order_number, client_name, product_name, quantity, unit_price, status, order_date, ship_date)
            VALUES (@owner, @number, 'Acme', 'Cable', 1, 1, 'Pending', now(), now())
            RETURNING id
            """,
            new NpgsqlParameter("owner", userId),
            new NpgsqlParameter("number", orderNumber));

    private static async Task<Dictionary<int, string>> ReadOrderNumbersAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT id, order_number FROM \"FiberOrders\"", connection);
        await using var reader = await command.ExecuteReaderAsync();
        var numbers = new Dictionary<int, string>();
        while (await reader.ReadAsync())
            numbers.Add(reader.GetInt32(0), reader.GetString(1));
        return numbers;
    }

    private static Task<string?> OrderNumberIndexDefinitionAsync(string connectionString) =>
        ScalarAsync<string>(
            connectionString,
            "SELECT indexdef FROM pg_indexes WHERE schemaname = 'public' AND tablename = 'FiberOrders' AND indexname = @name",
            new NpgsqlParameter("name", FiberOrderMapping.OrderNumberIndexName));

    // ---- helpers ----

    // Task.WhenAll rethrows only the first failure; when the callers fail for different reasons the first is not
    // always the cause, so this throws an AggregateException that carries the exceptions of every faulted task.
    private static async Task<T[]> AwaitAllReportingEveryFailureAsync<T>(params Task<T>[] tasks)
    {
        try
        {
            return await Task.WhenAll(tasks);
        }
        catch
        {
            var failures = tasks.Where(t => t.IsFaulted).SelectMany(t => t.Exception!.InnerExceptions).ToList();
            if (failures.Count == 0) throw;
            throw new AggregateException("Not every concurrent call succeeded.", failures);
        }
    }

    // A database made for one test and dropped with it. Pooling is off so no connection outlives the drop.
    private sealed class ScratchDatabase : IAsyncDisposable
    {
        private readonly string _serverConnectionString;
        private readonly string _name;

        private ScratchDatabase(string serverConnectionString, string name, string connectionString)
        {
            _serverConnectionString = serverConnectionString;
            _name = name;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<ScratchDatabase> CreateAsync(string server)
        {
            var serverConnectionString = new NpgsqlConnectionStringBuilder(server) { Pooling = false }.ConnectionString;
            var name = $"portfolio_pgtest_{Guid.NewGuid():N}";
            await ExecuteAsync(serverConnectionString, $"CREATE DATABASE \"{name}\"");
            var connectionString = new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = name }.ConnectionString;
            return new ScratchDatabase(serverConnectionString, name, connectionString);
        }

        // A failed drop is only noted: this runs when the test ends, and an exception from here would replace the
        // test's own failure with a cleanup error.
        public async ValueTask DisposeAsync()
        {
            try
            {
                await ExecuteAsync(_serverConnectionString, $"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)");
            }
            catch (Exception e)
            {
                TestContext.Current.TestOutputHelper?.WriteLine(
                    $"Could not drop the scratch database {_name}: {e.Message} Drop it by hand (see the class remarks).");
            }
        }

        // The name is a guid this class generated, so interpolating it into the statement carries no injection risk.
        private static async Task ExecuteAsync(string connectionString, string sql)
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    private sealed class InsertCounter
    {
        public int Attempts;
        public int Rejected;
    }

    // The real repository, plus a rendezvous after each caller's first read of the issued numbers and a count of inserts.
    private sealed class RendezvousRepository(IFiberOrderRepository inner, Barrier barrier, InsertCounter counter) : IFiberOrderRepository
    {
        private bool _rendezvousDone;

        public Task<List<FiberOrder>> GetAllAsync(Guid userId, CancellationToken cancellationToken = default) =>
            inner.GetAllAsync(userId, cancellationToken);

        public Task<FiberOrder?> GetByIdAsync(int id, Guid userId, CancellationToken cancellationToken = default) =>
            inner.GetByIdAsync(id, userId, cancellationToken);

        public async Task<List<string>> GetOrderNumbersAsync(Guid userId, string prefix, CancellationToken cancellationToken = default)
        {
            var numbers = await inner.GetOrderNumbersAsync(userId, prefix, cancellationToken);
            if (_rendezvousDone) return numbers;

            _rendezvousDone = true;
            var met = await Task.Run(() => barrier.SignalAndWait(TimeSpan.FromSeconds(30)), cancellationToken);
            if (!met) throw new TimeoutException("The other caller never reached the rendezvous.");
            return numbers;
        }

        public async Task<FiberOrder?> TryAddAsync(FiberOrder order, CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref counter.Attempts);
            var added = await inner.TryAddAsync(order, cancellationToken);
            if (added is null) Interlocked.Increment(ref counter.Rejected);
            return added;
        }

        public Task<FiberOrder> UpdateAsync(FiberOrder order, CancellationToken cancellationToken = default) =>
            inner.UpdateAsync(order, cancellationToken);

        public Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default) =>
            inner.DeleteAsync(id, userId, cancellationToken);
    }
}
