using Microsoft.EntityFrameworkCore.Metadata;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Repositories.Mappings;

namespace Portfolio.Tests.Repositories
{
    // The EF InMemory provider enforces neither unique indexes nor filters, so the mapping is checked on the
    // model. The SQL the index becomes is the generated migration's job.
    public class FiberOrderMappingTests
    {
        private static IIndex OrderNumberIndex()
        {
            using var db = new PortfolioDbContext(new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseInMemoryDatabase($"FiberOrderMapping_{Guid.NewGuid()}")
                .Options);
            var entity = db.Model.FindEntityType(typeof(FiberOrder))!;
            return Assert.Single(entity.GetIndexes(), i => i.IsUnique);
        }

        [Fact]
        public void OrderNumberIndex_IsUniqueOverUserAndOrderNumber()
        {
            // Arrange & Act
            var index = OrderNumberIndex();

            // Assert
            Assert.Equal([nameof(FiberOrder.UserId), nameof(FiberOrder.OrderNumber)], index.Properties.Select(p => p.Name).ToArray());
            Assert.True(index.IsUnique);
        }

        [Fact]
        public void OrderNumberIndex_ExemptsBlankOrderNumbersWithAFilter()
        {
            // Arrange & Act
            var index = OrderNumberIndex();

            // Assert
            Assert.Equal("\"order_number\" <> ''", index.GetFilter());
        }

        [Fact]
        public void OrderNumberIndex_UsesTheNameTheRepositoryMatches()
        {
            // Arrange & Act
            var index = OrderNumberIndex();

            // Assert
            Assert.Equal("IX_FiberOrders_user_id_order_number", FiberOrderMapping.OrderNumberIndexName);
            Assert.Equal(FiberOrderMapping.OrderNumberIndexName, index.GetDatabaseName());
        }

        [Fact]
        public void UserIdIndex_StillExistsAlongsideTheUniqueIndex()
        {
            // Arrange
            using var db = new PortfolioDbContext(new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseInMemoryDatabase($"FiberOrderMapping_{Guid.NewGuid()}")
                .Options);

            // Act
            var indexes = db.Model.FindEntityType(typeof(FiberOrder))!.GetIndexes().ToList();

            // Assert
            Assert.Contains(indexes, i => !i.IsUnique && i.Properties.Select(p => p.Name).SequenceEqual([nameof(FiberOrder.UserId)]));
        }
    }
}
