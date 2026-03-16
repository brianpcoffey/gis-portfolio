using Microsoft.EntityFrameworkCore;
using Portfolio.Common.Models;
using Portfolio.Repositories;
using Portfolio.Services.Services;
using Xunit;

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
            _service = new UserProfileSeedService(_dbContext);
        }

        [Fact]
        public async Task SeedForUserAsync_WhenNoData_CreatesEntities()
        {
            var userId = Guid.NewGuid();
            await _service.SeedForUserAsync(userId);
            Assert.True(await _dbContext.FiberClients.AnyAsync(c => c.UserId == userId));
            Assert.True(await _dbContext.FiberMaterials.AnyAsync(m => m.UserId == userId));
        }
    }
}
