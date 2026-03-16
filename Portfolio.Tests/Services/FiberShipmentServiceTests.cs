using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Xunit;

namespace Portfolio.Tests.Services
{
    public class FiberShipmentServiceTests
    {
        private readonly Mock<IFiberShipmentRepository> _shipmentRepoMock;
        private readonly Mock<IUserProfileService> _userProfileServiceMock;
        private readonly FakeTimeProvider _timeProvider;
        private readonly FiberShipmentService _service;
        private readonly Guid _testUserId = Guid.NewGuid();

        public FiberShipmentServiceTests()
        {
            _shipmentRepoMock = new Mock<IFiberShipmentRepository>();
            _userProfileServiceMock = new Mock<IUserProfileService>();
            _userProfileServiceMock.Setup(x => x.GetCurrentUserId()).Returns(_testUserId);
            _timeProvider = new FakeTimeProvider(DateTimeOffset.UtcNow);
            _service = new FiberShipmentService(_shipmentRepoMock.Object, _userProfileServiceMock.Object, _timeProvider);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsMappedDtos()
        {
            var shipments = new List<FiberShipment>
            {
                new() { Id = 1, UserId = _testUserId },
                new() { Id = 2, UserId = _testUserId }
            };
            _shipmentRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(shipments);
            var result = await _service.GetAllAsync();
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task UpdateStatusAsync_UpdatesStatus()
        {
            var shipment = new FiberShipment { Id = 1, UserId = _testUserId };
            _shipmentRepoMock.Setup(r => r.UpdateStatusAsync(1, It.IsAny<string>(), _testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);
            var dto = new UpdateShipmentStatusDto { Status = "Delivered" };
            var result = await _service.UpdateStatusAsync(1, dto);
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public async Task Throws_WhenUserNotIdentified()
        {
            _userProfileServiceMock.Setup(x => x.GetCurrentUserId()).Returns((Guid?)null);
            var service = new FiberShipmentService(_shipmentRepoMock.Object, _userProfileServiceMock.Object, _timeProvider);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAllAsync());
        }
    }
}
