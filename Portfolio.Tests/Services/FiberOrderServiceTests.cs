using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Xunit;

namespace Portfolio.Tests.Services
{
    public class FiberOrderServiceTests
    {
        private readonly Mock<IFiberOrderRepository> _orderRepoMock;
        private readonly Mock<IFiberClientRepository> _clientRepoMock;
        private readonly Mock<IUserProfileService> _userProfileServiceMock;
        private readonly FakeTimeProvider _timeProvider;
        private readonly FiberOrderService _service;
        private readonly Guid _testUserId = Guid.NewGuid();

        public FiberOrderServiceTests()
        {
            _orderRepoMock = new Mock<IFiberOrderRepository>();
            _clientRepoMock = new Mock<IFiberClientRepository>();
            _userProfileServiceMock = new Mock<IUserProfileService>();
            _userProfileServiceMock.Setup(x => x.GetCurrentUserId()).Returns(_testUserId);
            _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
            _service = new FiberOrderService(_orderRepoMock.Object, _clientRepoMock.Object, _userProfileServiceMock.Object, _timeProvider);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsMappedDtos()
        {
            var orders = new List<FiberOrder>
            {
                new() { Id = 1, UserId = _testUserId, OrderDate = DateTime.UtcNow, UnitPrice = 10, Quantity = 2 },
                new() { Id = 2, UserId = _testUserId, OrderDate = DateTime.UtcNow, UnitPrice = 20, Quantity = 1 }
            };
            _orderRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(orders);
            var result = await _service.GetAllAsync();
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetByIdAsync_WhenOrderExists_ReturnsDto()
        {
            var order = new FiberOrder { Id = 1, UserId = _testUserId, OrderDate = DateTime.UtcNow, UnitPrice = 10, Quantity = 2 };
            _orderRepoMock.Setup(r => r.GetByIdAsync(1, _testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
            var result = await _service.GetByIdAsync(1);
            Assert.NotNull(result);
            Assert.Equal(1, result!.Id);
        }

        [Fact]
        public async Task GetByIdAsync_WhenOrderNotExists_ReturnsNull()
        {
            _orderRepoMock.Setup(r => r.GetByIdAsync(999, _testUserId, It.IsAny<CancellationToken>())).ReturnsAsync((FiberOrder?)null);
            var result = await _service.GetByIdAsync(999);
            Assert.Null(result);
        }

        [Fact]
        public async Task Throws_WhenUserNotIdentified()
        {
            _userProfileServiceMock.Setup(x => x.GetCurrentUserId()).Returns((Guid?)null);
            var service = new FiberOrderService(_orderRepoMock.Object, _clientRepoMock.Object, _userProfileServiceMock.Object, _timeProvider);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAllAsync());
        }

        // ── CreateAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task CreateAsync_WithValidDto_ReturnsCreatedDto()
        {
            // Arrange
            var dto = new CreateFiberOrderDto
            {
                ClientName  = "Acme",
                ProductName = "Cable",
                Quantity    = 10,
                UnitPrice   = 5.00m,
                Status      = "Draft",
                OrderDate   = DateTime.UtcNow,
                ShipDate    = DateTime.UtcNow.AddDays(7)
            };
            var created = new FiberOrder
            {
                Id          = 42,
                UserId      = _testUserId,
                ClientName  = dto.ClientName,
                ProductName = dto.ProductName,
                Quantity    = dto.Quantity,
                UnitPrice   = dto.UnitPrice,
                Status      = dto.Status,
                OrderDate   = dto.OrderDate,
                ShipDate    = dto.ShipDate
            };
            _orderRepoMock
                .Setup(r => r.AddAsync(It.IsAny<FiberOrder>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(created);

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(42, result.Id);
            Assert.Equal("Acme", result.ClientName);
            _orderRepoMock.Verify(r => r.AddAsync(
                It.Is<FiberOrder>(o => o.UserId == _testUserId && o.ClientName == "Acme"),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateAsync_WhenUserNotIdentified_Throws()
        {
            // Arrange
            _userProfileServiceMock.Setup(x => x.GetCurrentUserId()).Returns((Guid?)null);
            var service = new FiberOrderService(_orderRepoMock.Object, _clientRepoMock.Object, _userProfileServiceMock.Object, _timeProvider);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateAsync(new CreateFiberOrderDto()));
        }

        // ── UpdateAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task UpdateAsync_WhenOrderExists_AppliesChangesAndReturnsDto()
        {
            // Arrange
            var existing = new FiberOrder
            {
                Id          = 1,
                UserId      = _testUserId,
                ClientName  = "Old Client",
                ProductName = "Old Product",
                Quantity    = 1,
                UnitPrice   = 10m,
                Status      = "Draft",
                OrderDate   = DateTime.UtcNow,
                ShipDate    = DateTime.UtcNow.AddDays(5)
            };
            var dto = new UpdateFiberOrderDto { ClientName = "New Client", Quantity = 5 };
            _orderRepoMock
                .Setup(r => r.GetByIdAsync(1, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            _orderRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<FiberOrder>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FiberOrder o, CancellationToken _) => o);

            // Act
            var result = await _service.UpdateAsync(1, dto);

            // Assert
            Assert.Equal("New Client", result.ClientName);
            Assert.Equal(5, result.Quantity);
            _orderRepoMock.Verify(r => r.UpdateAsync(
                It.Is<FiberOrder>(o => o.ClientName == "New Client" && o.Quantity == 5),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateAsync_WhenOrderNotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            _orderRepoMock
                .Setup(r => r.GetByIdAsync(999, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((FiberOrder?)null);

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _service.UpdateAsync(999, new UpdateFiberOrderDto()));
        }

        [Fact]
        public async Task UpdateAsync_OnlyUpdatesProvidedFields()
        {
            // Arrange
            var existing = new FiberOrder
            {
                Id          = 2,
                UserId      = _testUserId,
                ClientName  = "OriginalClient",
                ProductName = "OriginalProduct",
                Quantity    = 3,
                UnitPrice   = 20m,
                Status      = "Confirmed",
                OrderDate   = DateTime.UtcNow,
                ShipDate    = DateTime.UtcNow.AddDays(3)
            };
            // Only update Status; everything else must remain unchanged
            var dto = new UpdateFiberOrderDto { Status = "Shipped" };
            _orderRepoMock
                .Setup(r => r.GetByIdAsync(2, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            _orderRepoMock
                .Setup(r => r.UpdateAsync(It.IsAny<FiberOrder>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FiberOrder o, CancellationToken _) => o);

            // Act
            var result = await _service.UpdateAsync(2, dto);

            // Assert
            Assert.Equal("Shipped",          result.Status);
            Assert.Equal("OriginalClient",   result.ClientName);
            Assert.Equal("OriginalProduct",  result.ProductName);
            Assert.Equal(3,                  result.Quantity);
        }

        // ── DeleteAsync ─────────────────────────────────────────────────

        [Fact]
        public async Task DeleteAsync_WhenOrderExists_ReturnsTrue()
        {
            // Arrange
            _orderRepoMock
                .Setup(r => r.DeleteAsync(1, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.DeleteAsync(1);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task DeleteAsync_WhenOrderNotFound_ReturnsFalse()
        {
            // Arrange
            _orderRepoMock
                .Setup(r => r.DeleteAsync(999, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.DeleteAsync(999);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task DeleteAsync_WhenUserNotIdentified_Throws()
        {
            // Arrange
            _userProfileServiceMock.Setup(x => x.GetCurrentUserId()).Returns((Guid?)null);
            var service = new FiberOrderService(_orderRepoMock.Object, _clientRepoMock.Object, _userProfileServiceMock.Object, _timeProvider);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteAsync(1));
        }
    }
}
