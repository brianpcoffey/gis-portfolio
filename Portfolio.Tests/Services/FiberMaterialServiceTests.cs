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

        [Fact]
        public async Task GetByIdAsync_ReturnsMappedDto_WhenFound()
        {
            var material = new FiberMaterial { Id = 1, UserId = _testUserId };
            _materialRepoMock.Setup(r => r.GetByIdAsync(1, _testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(material);
            var result = await _service.GetByIdAsync(1);
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
        {
            _materialRepoMock.Setup(r => r.GetByIdAsync(1, _testUserId, It.IsAny<CancellationToken>())).ReturnsAsync((FiberMaterial)null);
            var result = await _service.GetByIdAsync(1);
            Assert.Null(result);
        }

        [Fact]
        public async Task CreateAsync_CreatesAndReturnsDto()
        {
            var dto = new FiberMaterialDto { Name = "Mat1", Sku = "SKU1", QtyOnHand = 10, UnitCost = 2, ReorderPoint = 5 };
            var created = new FiberMaterial { Id = 2, UserId = _testUserId, Name = "Mat1", Sku = "SKU1", QtyOnHand = 10, UnitCost = 2, ReorderPoint = 5 };
            _materialRepoMock.Setup(r => r.AddAsync(It.IsAny<FiberMaterial>(), It.IsAny<CancellationToken>())).ReturnsAsync(created);
            var result = await _service.CreateAsync(dto);
            Assert.Equal(2, result.Id);
            Assert.Equal("Mat1", result.Name);
        }

        [Fact]
        public async Task UpdateAsync_UpdatesAndReturnsDto()
        {
            var dto = new FiberMaterialDto { Name = "Mat2", Sku = "SKU2", QtyOnHand = 20, UnitCost = 3, ReorderPoint = 7 };
            var updated = new FiberMaterial { Id = 3, UserId = _testUserId, Name = "Mat2", Sku = "SKU2", QtyOnHand = 20, UnitCost = 3, ReorderPoint = 7 };
            _materialRepoMock.Setup(r => r.UpdateAsync(3, dto, _testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(updated);
            var result = await _service.UpdateAsync(3, dto);
            Assert.Equal(3, result.Id);
            Assert.Equal("Mat2", result.Name);
        }

        [Fact]
        public async Task DeleteAsync_DeletesAndReturnsTrue()
        {
            _materialRepoMock.Setup(r => r.DeleteAsync(4, _testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var result = await _service.DeleteAsync(4);
            Assert.True(result);
        }

        [Fact]
        public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
        {
            _materialRepoMock.Setup(r => r.DeleteAsync(5, _testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var result = await _service.DeleteAsync(5);
            Assert.False(result);
        }
    }
}
