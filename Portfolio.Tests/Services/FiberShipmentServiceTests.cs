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

        [Fact]
        public async Task GetByIdAsync_ReturnsMappedDto_WhenFound()
        {
            var shipment = new FiberShipment { Id = 1, UserId = _testUserId };
            _shipmentRepoMock.Setup(r => r.GetByIdAsync(1, _testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(shipment);
            var result = await _service.GetByIdAsync(1);
            Assert.NotNull(result);
            Assert.Equal(1, result.Id);
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsNull_WhenNotFound()
        {
            _shipmentRepoMock.Setup(r => r.GetByIdAsync(1, _testUserId, It.IsAny<CancellationToken>())).ReturnsAsync((FiberShipment)null);
            var result = await _service.GetByIdAsync(1);
            Assert.Null(result);
        }

        [Fact]
        public async Task CreateAsync_CreatesAndReturnsDto()
        {
            var dto = new FiberShipmentDto { TrackingNumber = "T1", CarrierName = "C1", Status = "S1" };
            var created = new FiberShipment { Id = 2, UserId = _testUserId, TrackingNumber = "T1", CarrierName = "C1", Status = "S1" };
            _shipmentRepoMock.Setup(r => r.AddAsync(It.IsAny<FiberShipment>(), It.IsAny<CancellationToken>())).ReturnsAsync(created);
            var result = await _service.CreateAsync(dto);
            Assert.Equal(2, result.Id);
            Assert.Equal("T1", result.TrackingNumber);
        }

        [Fact]
        public async Task UpdateAsync_UpdatesAndReturnsDto()
        {
            var dto = new FiberShipmentDto { TrackingNumber = "T2", CarrierName = "C2", Status = "S2" };
            var updated = new FiberShipment { Id = 3, UserId = _testUserId, TrackingNumber = "T2", CarrierName = "C2", Status = "S2" };
            _shipmentRepoMock.Setup(r => r.UpdateAsync(3, dto, _testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(updated);
            var result = await _service.UpdateAsync(3, dto);
            Assert.Equal(3, result.Id);
            Assert.Equal("T2", result.TrackingNumber);
        }

        [Fact]
        public async Task DeleteAsync_DeletesAndReturnsTrue()
        {
            _shipmentRepoMock.Setup(r => r.DeleteAsync(4, _testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
            var result = await _service.DeleteAsync(4);
            Assert.True(result);
        }

        [Fact]
        public async Task DeleteAsync_ReturnsFalse_WhenNotFound()
        {
            _shipmentRepoMock.Setup(r => r.DeleteAsync(5, _testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(false);
            var result = await _service.DeleteAsync(5);
            Assert.False(result);
        }
    }
}
