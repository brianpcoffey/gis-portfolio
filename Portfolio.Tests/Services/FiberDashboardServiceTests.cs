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
        public async Task GetDashboardAsync_ReturnsDashboardDto_WithInventoryByCategory()
        {
            var materials = new List<Portfolio.Common.Models.FiberMaterial>
            {
                new Portfolio.Common.Models.FiberMaterial { Id = 1, UserId = _testUserId, Name = "Mat1", Sku = "A", Category = "Reinforcements", QtyOnHand = 10, ReorderPoint = 2, ReorderQty = 5, UnitCost = 1, Supplier = "S1", WarehouseLocation = "W1", LastUpdated = System.DateTime.UtcNow },
                new Portfolio.Common.Models.FiberMaterial { Id = 2, UserId = _testUserId, Name = "Mat2", Sku = "B", Category = "Resins", QtyOnHand = 5, ReorderPoint = 1, ReorderQty = 2, UnitCost = 2, Supplier = "S2", WarehouseLocation = "W2", LastUpdated = System.DateTime.UtcNow },
                new Portfolio.Common.Models.FiberMaterial { Id = 3, UserId = _testUserId, Name = "Mat3", Sku = "C", Category = "Reinforcements", QtyOnHand = 7, ReorderPoint = 2, ReorderQty = 3, UnitCost = 3, Supplier = "S3", WarehouseLocation = "W3", LastUpdated = System.DateTime.UtcNow }
            };
            _orderRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Portfolio.Common.Models.FiberOrder>());
            _shipmentRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Portfolio.Common.Models.FiberShipment>());
            _materialRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(materials);
            var result = await _service.GetDashboardAsync();
            Assert.NotNull(result);
            Assert.NotNull(result.InventoryByCategory);
            Assert.Contains(result.InventoryByCategory, c => c.Category == "Reinforcements" && c.Count == 2);
            Assert.Contains(result.InventoryByCategory, c => c.Category == "Resins" && c.Count == 1);
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
