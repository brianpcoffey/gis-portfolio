using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Repositories.Mappings;
using Portfolio.Repositories.Repositories;

namespace Portfolio.Tests.Repositories
{
    public class FiberOrderRepositoryTests
    {
        private static PortfolioDbContext NewDb() =>
            new(new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseInMemoryDatabase($"FiberOrderRepo_{Guid.NewGuid()}")
                .Options);

        // Fails the first save with the given exception, the way Postgres would reject a duplicate, then lets saves through.
        private sealed class FailFirstSaveInterceptor(Exception failure) : SaveChangesInterceptor
        {
            public int Saves { get; private set; }

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                Saves++;
                if (Saves == 1) throw failure;
                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }

        private static PortfolioDbContext NewDb(Exception failFirstSaveWith) =>
            new(new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseInMemoryDatabase($"FiberOrderRepo_{Guid.NewGuid()}")
                .AddInterceptors(new FailFirstSaveInterceptor(failFirstSaveWith))
                .Options);

        private static DbUpdateException UniqueViolation(string? constraintName) =>
            new("duplicate", new PostgresException(
                "duplicate key value violates unique constraint", "ERROR", "ERROR",
                PostgresErrorCodes.UniqueViolation, constraintName: constraintName));

        private static FiberOrder Order(Guid userId, string orderNumber) => new()
        {
            UserId = userId,
            OrderNumber = orderNumber,
            ClientName = "Acme",
            ProductName = "Cable"
        };

        [Fact]
        public async Task GetOrderNumbersAsync_ReturnsOnlyTheUsersNumbersThatStartWithThePrefix()
        {
            // Arrange
            using var db = NewDb();
            var user = Guid.NewGuid();
            var otherUser = Guid.NewGuid();
            db.FiberOrders.AddRange(
                Order(user, "ORD-20261013-0001"),
                Order(user, "ORD-20261013-0002"),
                Order(user, "ORD-20261012-0001"),
                Order(user, string.Empty),
                Order(otherUser, "ORD-20261013-0001"));
            await db.SaveChangesAsync();
            var repo = new FiberOrderRepository(db);

            // Act
            var numbers = await repo.GetOrderNumbersAsync(user, "ORD-20261013-");

            // Assert
            Assert.Equal(["ORD-20261013-0001", "ORD-20261013-0002"], numbers.Order().ToList());
        }

        [Fact]
        public async Task TryAddAsync_WithNoClash_PersistsAndReturnsTheOrder()
        {
            // Arrange
            using var db = NewDb();
            var repo = new FiberOrderRepository(db);
            var user = Guid.NewGuid();

            // Act
            var added = await repo.TryAddAsync(Order(user, "ORD-20261013-0001"));

            // Assert
            Assert.NotNull(added);
            Assert.NotEqual(0, added.Id);
            Assert.Equal("ORD-20261013-0001", Assert.Single(db.FiberOrders).OrderNumber);
        }

        [Fact]
        public async Task TryAddAsync_WhenTheOrderNumberIsAlreadyTaken_ReturnsNullAndStopsTrackingTheOrder()
        {
            // Arrange
            using var db = NewDb(UniqueViolation(FiberOrderMapping.OrderNumberIndexName));
            var repo = new FiberOrderRepository(db);
            var rejected = Order(Guid.NewGuid(), "ORD-20261013-0001");

            // Act
            var added = await repo.TryAddAsync(rejected);

            // Assert
            Assert.Null(added);
            Assert.Equal(EntityState.Detached, db.Entry(rejected).State);
            Assert.Empty(db.ChangeTracker.Entries<FiberOrder>());
            Assert.Empty(db.FiberOrders);
        }

        [Fact]
        public async Task TryAddAsync_AfterAClash_AddsTheNextOrderAndPersistsExactlyOneRow()
        {
            // Arrange
            using var db = NewDb(UniqueViolation(FiberOrderMapping.OrderNumberIndexName));
            var repo = new FiberOrderRepository(db);
            var user = Guid.NewGuid();
            Assert.Null(await repo.TryAddAsync(Order(user, "ORD-20261013-0001")));

            // Act
            var added = await repo.TryAddAsync(Order(user, "ORD-20261013-0002"));

            // Assert
            Assert.NotNull(added);
            Assert.Equal("ORD-20261013-0002", added.OrderNumber);
            Assert.Equal("ORD-20261013-0002", Assert.Single(db.FiberOrders).OrderNumber);
        }

        [Fact]
        public async Task TryAddAsync_WhenADifferentConstraintIsViolated_Rethrows()
        {
            // Arrange
            var failure = UniqueViolation("IX_SomeOtherIndex");
            using var db = NewDb(failure);
            var repo = new FiberOrderRepository(db);

            // Act
            var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => repo.TryAddAsync(Order(Guid.NewGuid(), "ORD-20261013-0001")));

            // Assert
            Assert.Same(failure, thrown);
        }

        [Fact]
        public async Task TryAddAsync_WhenTheViolationNamesNoConstraint_Rethrows()
        {
            // Arrange
            var failure = UniqueViolation(null);
            using var db = NewDb(failure);
            var repo = new FiberOrderRepository(db);

            // Act
            var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => repo.TryAddAsync(Order(Guid.NewGuid(), "ORD-20261013-0001")));

            // Assert
            Assert.Same(failure, thrown);
        }

        [Fact]
        public async Task TryAddAsync_WhenADifferentSqlStateIsRaisedOnTheSameConstraintName_Rethrows()
        {
            // Arrange
            var failure = new DbUpdateException("fk", new PostgresException(
                "violates foreign key constraint", "ERROR", "ERROR", PostgresErrorCodes.ForeignKeyViolation,
                constraintName: FiberOrderMapping.OrderNumberIndexName));
            using var db = NewDb(failure);
            var repo = new FiberOrderRepository(db);

            // Act
            var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => repo.TryAddAsync(Order(Guid.NewGuid(), "ORD-20261013-0001")));

            // Assert
            Assert.Same(failure, thrown);
        }

        [Fact]
        public async Task TryAddAsync_WhenTheInnerExceptionIsNotFromPostgres_Rethrows()
        {
            // Arrange
            var failure = new DbUpdateException("boom", new InvalidOperationException("not postgres"));
            using var db = NewDb(failure);
            var repo = new FiberOrderRepository(db);

            // Act
            var thrown = await Assert.ThrowsAsync<DbUpdateException>(() => repo.TryAddAsync(Order(Guid.NewGuid(), "ORD-20261013-0001")));

            // Assert
            Assert.Same(failure, thrown);
        }

        [Fact]
        public async Task GetOrderNumbersAsync_WithNoMatches_ReturnsAnEmptyList()
        {
            // Arrange
            using var db = NewDb();
            var repo = new FiberOrderRepository(db);

            // Act
            var numbers = await repo.GetOrderNumbersAsync(Guid.NewGuid(), "ORD-20261013-");

            // Assert
            Assert.Empty(numbers);
        }
    }
}
