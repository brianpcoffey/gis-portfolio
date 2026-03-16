using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Xunit;

namespace Portfolio.Tests.Services
{
    public class FiberDashboardServiceTests
    {
        private readonly Mock<IFiberOrderRepository> _orderRepoMock;
        private readonly Mock<IFiberShipmentRepository> _shipmentRepoMock;
        private readonly Mock<IFiberMaterialRepository> _materialRepoMock;
        private readonly Mock<IUserProfileService> _userProfileServiceMock;
        private readonly FiberDashboardService _service;
        private readonly Guid _testUserId = Guid.NewGuid();

        public FiberDashboardServiceTests()
        {
            _orderRepoMock = new Mock<IFiberOrderRepository>();
            _shipmentRepoMock = new Mock<IFiberShipmentRepository>();
            _materialRepoMock = new Mock<IFiberMaterialRepository>();
            _userProfileServiceMock = new Mock<IUserProfileService>();
            _userProfileServiceMock.Setup(x => x.GetCurrentUserId()).Returns(_testUserId);
            _service = new FiberDashboardService(_orderRepoMock.Object, _shipmentRepoMock.Object, _materialRepoMock.Object, _userProfileServiceMock.Object);
        }

        [Fact]
        public async Task GetDashboardAsync_ReturnsDashboardDto()
        {
            _orderRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Portfolio.Common.Models.FiberOrder>());
            _shipmentRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Portfolio.Common.Models.FiberShipment>());
            _materialRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Portfolio.Common.Models.FiberMaterial>());
            var result = await _service.GetDashboardAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task Throws_WhenUserNotIdentified()
        {
            _userProfileServiceMock.Setup(x => x.GetCurrentUserId()).Returns((Guid?)null);
            var service = new FiberDashboardService(_orderRepoMock.Object, _shipmentRepoMock.Object, _materialRepoMock.Object, _userProfileServiceMock.Object);
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetDashboardAsync());
        }
    }
}
