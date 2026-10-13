using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Repositories.Repositories;

namespace Portfolio.Tests.Repositories
{
    public class FiberOrderRepositoryTests
    {
        private static PortfolioDbContext NewDb() =>
            new(new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseInMemoryDatabase($"FiberOrderRepo_{Guid.NewGuid()}")
                .Options);

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
