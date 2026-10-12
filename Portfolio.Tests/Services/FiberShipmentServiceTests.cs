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
            _shipmentRepoMock.Setup(r => r.GetByIdAsync(1, _testUserId, It.IsAny<CancellationToken>())).ReturnsAsync((FiberShipment?)null);
            var result = await _service.GetByIdAsync(1);
            Assert.Null(result);
        }

        [Fact]
        public async Task CreateAsync_CreatesAndReturnsDto()
        {
            var dto = new FiberShipmentDto { TrackingNumber = "T1", CarrierName = "C1", Status = "Pending" };
            var created = new FiberShipment { Id = 2, UserId = _testUserId, TrackingNumber = "T1", CarrierName = "C1", Status = "Pending" };
            _shipmentRepoMock.Setup(r => r.AddAsync(It.IsAny<FiberShipment>(), It.IsAny<CancellationToken>())).ReturnsAsync(created);
            var result = await _service.CreateAsync(dto);
            Assert.Equal(2, result.Id);
            Assert.Equal("T1", result.TrackingNumber);
        }

        [Fact]
        public async Task UpdateAsync_UpdatesAndReturnsDto()
        {
            var dto = new FiberShipmentDto { TrackingNumber = "T2", CarrierName = "C2", Status = "Delivered" };
            var updated = new FiberShipment { Id = 3, UserId = _testUserId, TrackingNumber = "T2", CarrierName = "C2", Status = "Delivered" };
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

        // â”€â”€ Persistence limits: values exactly at the boundary are accepted â”€â”€

        private static FiberShipmentDto ValidShipment() => new()
        {
            TrackingNumber = "T1",
            CarrierName = "C1",
            Status = "Pending",
            DestinationCity = "Redlands",
            DestinationState = "CA",
            Route = new List<RoutePointDto> { new() { Lat = 34.0, Lng = -117.0 } }
        };

        private void SetupShipmentAdd() =>
            _shipmentRepoMock
                .Setup(r => r.AddAsync(It.IsAny<FiberShipment>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((FiberShipment s, CancellationToken _) => s);

        [Theory]
        [InlineData("Pending")]
        [InlineData("In Transit")]
        [InlineData("Delivered")]
        [InlineData("Exception")]
        public async Task CreateAsync_AcceptsEachAllowedStatus(string status)
        {
            SetupShipmentAdd();
            var dto = ValidShipment();
            dto.Status = status;

            var result = await _service.CreateAsync(dto);

            Assert.Equal(status, result.Status);
        }

        [Fact]
        public async Task CreateAsync_AcceptsFieldsAtMaximumLength()
        {
            SetupShipmentAdd();
            var dto = ValidShipment();
            dto.TrackingNumber = new string('t', 100);
            dto.CarrierName = new string('c', 100);
            dto.DestinationCity = new string('y', 100);
            dto.DestinationState = new string('s', 50);
            dto.Route = Enumerable.Range(0, 1000).Select(i => new RoutePointDto { Lat = 34.0, Lng = -117.0 }).ToList();

            var result = await _service.CreateAsync(dto);

            Assert.Equal(1000, result.Route.Count);
            Assert.Equal(100, result.TrackingNumber.Length);
        }

        // â”€â”€ Persistence limits: one over the boundary is a 400 (ArgumentException) â”€â”€

        [Fact]
        public async Task CreateAsync_RejectsTrackingNumberOverLimit()
        {
            var dto = ValidShipment();
            dto.TrackingNumber = new string('t', 101);
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
            Assert.StartsWith("TrackingNumber must be 100 characters or fewer.", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_RejectsCarrierNameOverLimit()
        {
            var dto = ValidShipment();
            dto.CarrierName = new string('c', 101);
            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
        }

        [Fact]
        public async Task CreateAsync_RejectsDestinationCityOverLimit()
        {
            var dto = ValidShipment();
            dto.DestinationCity = new string('y', 101);
            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
        }

        [Fact]
        public async Task CreateAsync_RejectsDestinationStateOverLimit()
        {
            var dto = ValidShipment();
            dto.DestinationState = new string('s', 51);
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
            Assert.StartsWith("DestinationState must be 50 characters or fewer.", ex.Message);
        }

        [Theory]
        [InlineData("")]
        [InlineData("pending")]
        [InlineData("InTransit")]
        [InlineData("Lost")]
        public async Task CreateAsync_RejectsStatusOutsideAllowlist(string status)
        {
            var dto = ValidShipment();
            dto.Status = status;
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
            Assert.StartsWith("Status must be one of: Pending, In Transit, Delivered, Exception.", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_RejectsRouteOverLimit()
        {
            var dto = ValidShipment();
            dto.Route = Enumerable.Range(0, 1001).Select(i => new RoutePointDto { Lat = 34.0, Lng = -117.0 }).ToList();
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
            Assert.StartsWith("Route can have at most 1000 points.", ex.Message);
        }

        [Fact]
        public async Task CreateAsync_RejectsNonFiniteCoordinates()
        {
            var dto = ValidShipment();
            dto.OriginLat = double.NaN;
            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));

            dto = ValidShipment();
            dto.Route = new List<RoutePointDto> { new() { Lat = 1, Lng = double.PositiveInfinity } };
            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
        }

        [Fact]
        public async Task UpdateAsync_RejectsInvalidShipment_WithoutCallingRepository()
        {
            var dto = ValidShipment();
            dto.Status = "Lost";

            await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateAsync(3, dto));

            _shipmentRepoMock.Verify(r => r.UpdateAsync(It.IsAny<int>(), It.IsAny<FiberShipmentDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateStatusAsync_RejectsStatusOutsideAllowlist()
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.UpdateStatusAsync(1, new UpdateShipmentStatusDto { Status = "Lost" }));

            _shipmentRepoMock.Verify(r => r.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
