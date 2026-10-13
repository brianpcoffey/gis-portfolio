using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Services.Services;
using Portfolio.Tests.Repositories;

namespace Portfolio.Tests.Services
{
    public class UserProfileSeedServiceTests
    {
        private readonly PortfolioDbContext _dbContext;
        private readonly UserProfileSeedService _service;

        public UserProfileSeedServiceTests()
        {
            var dbOptions = new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseInMemoryDatabase(databaseName: $"TestDb_{Guid.NewGuid()}")
                .Options;
            _dbContext = new PortfolioDbContext(dbOptions);
            _service = new UserProfileSeedService(_dbContext, NullLogger<UserProfileSeedService>.Instance);
        }

        [Fact]
        public async Task SeedForUserAsync_WhenNoData_CreatesEntities()
        {
            var userId = Guid.NewGuid();
            await _service.SeedForUserAsync(userId);
            Assert.True(await _dbContext.FiberClients.AnyAsync(c => c.UserId == userId));
            Assert.True(await _dbContext.FiberMaterials.AnyAsync(m => m.UserId == userId));
        }

        // PUT lets a user change a demo order's client or product. The unique (user, order number) index would reject a
        // second copy of that order's number, which would roll the seed back on every later sign-in, so the seed
        // recognises the order by its number as well. (EF InMemory cannot enforce the index; the assertions on the
        // row count and the numbers stand in for it.)
        [Theory]
        [InlineData(nameof(FiberOrder.ProductName))]
        [InlineData(nameof(FiberOrder.ClientName))]
        public async Task SeedForUserAsync_WhenADemoOrderWasEdited_DoesNotSeedItAgainOrLogAnError(string editedField)
        {
            // Arrange: a fully seeded user, then one demo order edited the way PUT allows.
            var userId = Guid.NewGuid();
            var logger = new CapturingLogger<UserProfileSeedService>();
            var service = new UserProfileSeedService(_dbContext, logger);
            await service.SeedForUserAsync(userId);
            var edited = await _dbContext.FiberOrders.Where(o => o.UserId == userId).OrderBy(o => o.Id).FirstAsync();
            var editedNumber = edited.OrderNumber;
            if (editedField == nameof(FiberOrder.ProductName))
                edited.ProductName = "Edited product";
            else
                edited.ClientName = "Edited client";
            await _dbContext.SaveChangesAsync();

            // Act: the next sign-in seeds again.
            await service.SeedForUserAsync(userId);

            // Assert
            var numbers = await _dbContext.FiberOrders.Where(o => o.UserId == userId).Select(o => o.OrderNumber).ToListAsync();
            Assert.Equal(17, numbers.Count);
            Assert.Equal(17, numbers.Distinct().Count());
            Assert.Contains(editedNumber, numbers);
            Assert.DoesNotContain(logger.Entries, e => e.Level >= LogLevel.Warning);
        }
    }
}
