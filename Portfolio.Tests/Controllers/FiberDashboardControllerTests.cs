using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;
using Xunit;

namespace Portfolio.Tests.Controllers
{
    public class FiberDashboardControllerTests
    {
        private readonly Mock<IFiberDashboardService> _serviceMock;
        private readonly FiberDashboardController _controller;

        public FiberDashboardControllerTests()
        {
            _serviceMock = new Mock<IFiberDashboardService>();
            _controller  = new FiberDashboardController(
                _serviceMock.Object);
        }

        // ── GetStats ────────────────────────────────────────────────────

        [Fact]
        public async Task GetStats_ReturnsOkWithDashboardDto()
        {
            // Arrange
            var dto = new FiberDashboardDto
            {
                ActiveShipments  = 5,
                OpenOrders       = 12,
                LowStockAlerts   = 3,
                MtdRevenue       = 45000m,
                RevenueByMonth   = [new() { Month = "Jan", Revenue = 15000m }],
                OrdersByStatus   = [new() { Status = "Open", Count = 12 }],
                TopClients       = [new() { Name = "Acme", Revenue = 20000m }],
                InventoryByCategory = [new() { Category = "Cable", Count = 200 }]
            };
            _serviceMock.Setup(s => s.GetDashboardAsync(It.IsAny<CancellationToken>())).ReturnsAsync(dto);

            // Act
            var result = await _controller.GetStats(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(dto, ok.Value);
        }

        [Fact]
        public async Task GetStats_WhenServiceThrows_PropagatesTheException()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetDashboardAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("db error"));

            // Act & Assert
            await Assert.ThrowsAsync<Exception>(() => _controller.GetStats(CancellationToken.None));
        }

    }
}
