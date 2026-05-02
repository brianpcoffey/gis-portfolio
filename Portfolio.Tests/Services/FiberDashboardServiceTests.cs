using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using System.Linq;
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

        // ── Active shipments ────────────────────────────────────────────

        [Fact]
        public async Task GetDashboardAsync_CountsActiveShipmentsCorrectly()
        {
            // Arrange
            var shipments = new List<Portfolio.Common.Models.FiberShipment>
            {
                new() { Id = 1, UserId = _testUserId, Status = "In Transit" },
                new() { Id = 2, UserId = _testUserId, Status = "In Transit" },
                new() { Id = 3, UserId = _testUserId, Status = "Delivered" }
            };
            _orderRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberOrder>());
            _shipmentRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(shipments);
            _materialRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberMaterial>());

            // Act
            var result = await _service.GetDashboardAsync();

            // Assert
            Assert.Equal(2, result.ActiveShipments);
        }

        // ── Open orders ────────────────────────────────────────────────

        [Fact]
        public async Task GetDashboardAsync_CountsOpenOrdersExcludingDeliveredAndShipped()
        {
            // Arrange
            var orders = new List<Portfolio.Common.Models.FiberOrder>
            {
                new() { Id = 1, UserId = _testUserId, Status = "Draft",       OrderDate = DateTime.UtcNow, UnitPrice = 1, Quantity = 1 },
                new() { Id = 2, UserId = _testUserId, Status = "Confirmed",   OrderDate = DateTime.UtcNow, UnitPrice = 1, Quantity = 1 },
                new() { Id = 3, UserId = _testUserId, Status = "Shipped",     OrderDate = DateTime.UtcNow, UnitPrice = 1, Quantity = 1 },
                new() { Id = 4, UserId = _testUserId, Status = "Delivered",   OrderDate = DateTime.UtcNow, UnitPrice = 1, Quantity = 1 }
            };
            _orderRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(orders);
            _shipmentRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberShipment>());
            _materialRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberMaterial>());

            // Act
            var result = await _service.GetDashboardAsync();

            // Assert
            Assert.Equal(2, result.OpenOrders);
        }

        // ── Low stock alerts ───────────────────────────────────────────

        [Fact]
        public async Task GetDashboardAsync_CountsLowStockAlertsCorrectly()
        {
            // Arrange
            var materials = new List<Portfolio.Common.Models.FiberMaterial>
            {
                new() { Id = 1, UserId = _testUserId, Name = "A", Sku = "A", QtyOnHand = 1, ReorderPoint = 5,  Category = "X", ReorderQty = 1, UnitCost = 1, Supplier = "S", WarehouseLocation = "W", LastUpdated = DateTime.UtcNow },
                new() { Id = 2, UserId = _testUserId, Name = "B", Sku = "B", QtyOnHand = 10, ReorderPoint = 5, Category = "X", ReorderQty = 1, UnitCost = 1, Supplier = "S", WarehouseLocation = "W", LastUpdated = DateTime.UtcNow },
                new() { Id = 3, UserId = _testUserId, Name = "C", Sku = "C", QtyOnHand = 5, ReorderPoint = 5,  Category = "X", ReorderQty = 1, UnitCost = 1, Supplier = "S", WarehouseLocation = "W", LastUpdated = DateTime.UtcNow }
            };
            _orderRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberOrder>());
            _shipmentRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberShipment>());
            _materialRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(materials);

            // Act
            var result = await _service.GetDashboardAsync();

            // Assert - QtyOnHand <= ReorderPoint: items A (1<=5) and C (5<=5)
            Assert.Equal(2, result.LowStockAlerts);
        }

        // ── MTD revenue ────────────────────────────────────────────────

        [Fact]
        public async Task GetDashboardAsync_CalculatesMtdRevenueFromCurrentMonthOrdersOnly()
        {
            // Arrange
            var now      = DateTime.UtcNow;
            var mtdOrder = new Portfolio.Common.Models.FiberOrder
            {
                Id = 1, UserId = _testUserId, Status = "Confirmed",
                OrderDate = new DateTime(now.Year, now.Month, 1),
                UnitPrice = 100m, Quantity = 3
            };
            var oldOrder = new Portfolio.Common.Models.FiberOrder
            {
                Id = 2, UserId = _testUserId, Status = "Delivered",
                OrderDate = now.AddMonths(-1),
                UnitPrice = 50m, Quantity = 10
            };
            _orderRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberOrder> { mtdOrder, oldOrder });
            _shipmentRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberShipment>());
            _materialRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberMaterial>());

            // Act
            var result = await _service.GetDashboardAsync();

            // Assert — only the current-month order (100 * 3 = 300) counts
            Assert.Equal(300m, result.MtdRevenue);
        }

        // ── Orders by status ───────────────────────────────────────────

        [Fact]
        public async Task GetDashboardAsync_ReturnsAllKnownStatusesInOrdersByStatus()
        {
            // Arrange
            _orderRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberOrder>());
            _shipmentRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberShipment>());
            _materialRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberMaterial>());

            // Act
            var result = await _service.GetDashboardAsync();

            // Assert — all 5 known statuses appear even with no orders
            var statuses = result.OrdersByStatus.Select(s => s.Status).ToList();
            Assert.Contains("Draft",         statuses);
            Assert.Contains("Confirmed",     statuses);
            Assert.Contains("In Production", statuses);
            Assert.Contains("Shipped",       statuses);
            Assert.Contains("Delivered",     statuses);
            Assert.All(result.OrdersByStatus, s => Assert.Equal(0, s.Count));
        }

        [Fact]
        public async Task GetDashboardAsync_CountsOrdersByStatusCorrectly()
        {
            // Arrange
            var orders = new List<Portfolio.Common.Models.FiberOrder>
            {
                new() { Id = 1, UserId = _testUserId, Status = "Draft",     OrderDate = DateTime.UtcNow, UnitPrice = 1, Quantity = 1 },
                new() { Id = 2, UserId = _testUserId, Status = "Draft",     OrderDate = DateTime.UtcNow, UnitPrice = 1, Quantity = 1 },
                new() { Id = 3, UserId = _testUserId, Status = "Confirmed", OrderDate = DateTime.UtcNow, UnitPrice = 1, Quantity = 1 }
            };
            _orderRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(orders);
            _shipmentRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberShipment>());
            _materialRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberMaterial>());

            // Act
            var result = await _service.GetDashboardAsync();

            // Assert
            Assert.Equal(2, result.OrdersByStatus.First(s => s.Status == "Draft").Count);
            Assert.Equal(1, result.OrdersByStatus.First(s => s.Status == "Confirmed").Count);
        }

        // ── Top clients ────────────────────────────────────────────────

        [Fact]
        public async Task GetDashboardAsync_ReturnsTopFiveClientsByRevenue()
        {
            // Arrange — create 6 clients with known revenues in current year
            var now = DateTime.UtcNow;
            var orders = Enumerable.Range(1, 6).Select(i => new Portfolio.Common.Models.FiberOrder
            {
                Id         = i,
                UserId     = _testUserId,
                ClientName = $"Client{i}",
                UnitPrice  = i * 100m,
                Quantity   = 1,
                OrderDate  = new DateTime(now.Year, 1, 1),
                Status     = "Delivered"
            }).ToList();
            _orderRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(orders);
            _shipmentRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberShipment>());
            _materialRepoMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Portfolio.Common.Models.FiberMaterial>());

            // Act
            var result = await _service.GetDashboardAsync();

            // Assert — no more than 5, and highest revenue client is present
            Assert.True(result.TopClients.Count <= 5);
            Assert.Contains(result.TopClients, c => c.Name == "Client6" && c.Revenue == 600m);
            Assert.DoesNotContain(result.TopClients, c => c.Name == "Client1");
        }
    }
}
