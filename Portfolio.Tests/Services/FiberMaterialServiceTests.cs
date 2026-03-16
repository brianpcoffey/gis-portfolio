using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Repositories;
using Portfolio.Services.Services;
using Xunit;
using Portfolio.Services.Interfaces;

namespace Portfolio.Tests.Services
{
    public class FiberMaterialServiceTests
    {
        private readonly Mock<IFiberMaterialRepository> _materialRepoMock;
        private readonly Mock<IFiberInventoryTransactionRepository> _transactionRepoMock;
        private readonly Mock<IUserProfileService> _userProfileServiceMock;
        private readonly FakeTimeProvider _timeProvider;
        private readonly Mock<PortfolioDbContext> _dbContextMock;
        private readonly FiberMaterialService _service;
        private readonly Guid _testUserId = Guid.NewGuid();

        public FiberMaterialServiceTests()
        {
            _materialRepoMock = new Mock<IFiberMaterialRepository>();
            _transactionRepoMock = new Mock<IFiberInventoryTransactionRepository>();
            _userProfileServiceMock = new Mock<IUserProfileService>();
            _userProfileServiceMock.Setup(x => x.GetCurrentUserId()).Returns(_testUserId);
            _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
            var dbOptions = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<PortfolioDbContext>().Options;
            _dbContextMock = new Mock<PortfolioDbContext>(dbOptions);
            _service = new FiberMaterialService(_materialRepoMock.Object, _transactionRepoMock.Object, _userProfileServiceMock.Object, _timeProvider, _dbContextMock.Object);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsMappedDtos()
        {
            var materials = new List<FiberMaterial>
            {
                new() { Id = 1, UserId = _testUserId },
                new() { Id = 2, UserId = _testUserId }
            };
            _materialRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(materials);
            var result = await _service.GetAllAsync();
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task Throws_WhenUserNotIdentified()
        {
            _userProfileServiceMock.Setup(x => x.GetCurrentUserId()).Returns((Guid?)null);
            var service = new FiberMaterialService(_materialRepoMock.Object, _transactionRepoMock.Object, _userProfileServiceMock.Object, _timeProvider, _dbContextMock.Object);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAllAsync());
        }
    }
}
