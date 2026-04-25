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
    }
}
