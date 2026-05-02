using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;
using Xunit;

namespace Portfolio.Tests.Controllers
{
    public class FiberShipmentsControllerTests
    {
        private readonly Mock<IFiberShipmentService> _serviceMock;
        private readonly FiberShipmentsController _controller;

        public FiberShipmentsControllerTests()
        {
            _serviceMock = new Mock<IFiberShipmentService>();
            _controller  = new FiberShipmentsController(
                _serviceMock.Object,
                new Mock<ILogger<FiberShipmentsController>>().Object);
        }

        // ── GetAll ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetAll_ReturnsOkWithList()
        {
            // Arrange
            var shipments = new List<FiberShipmentDto> { new() { Id = 1, TrackingNumber = "TRACK001" } };
            _serviceMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(shipments);

            // Act
            var result = await _controller.GetAll(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(shipments, ok.Value);
        }

        // ── Get ─────────────────────────────────────────────────────────

        [Fact]
        public async Task Get_WhenFound_ReturnsOk()
        {
            // Arrange
            var dto = new FiberShipmentDto { Id = 3, TrackingNumber = "TRACK003" };
            _serviceMock.Setup(s => s.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

            // Act
            var result = await _controller.Get(3, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(dto, ok.Value);
        }

        [Fact]
        public async Task Get_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((FiberShipmentDto?)null);

            // Act
            var result = await _controller.Get(99, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // ── Create ──────────────────────────────────────────────────────

        [Fact]
        public async Task Create_WithValidDto_ReturnsOkWithCreated()
        {
            // Arrange
            var dto     = new FiberShipmentDto { TrackingNumber = "NEW001", CarrierName = "FedEx", Status = "Pending" };
            var created = new FiberShipmentDto { Id = 9, TrackingNumber = "NEW001" };
            _serviceMock.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>())).ReturnsAsync(created);

            // Act
            var result = await _controller.Create(dto, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(created, ok.Value);
        }

        // ── Update ──────────────────────────────────────────────────────

        [Fact]
        public async Task Update_WhenSuccessful_ReturnsOkWithUpdated()
        {
            // Arrange
            var dto     = new FiberShipmentDto { TrackingNumber = "UPD001", Status = "InTransit" };
            var updated = new FiberShipmentDto { Id = 2, TrackingNumber = "UPD001", Status = "InTransit" };
            _serviceMock.Setup(s => s.UpdateAsync(2, dto, It.IsAny<CancellationToken>())).ReturnsAsync(updated);

            // Act
            var result = await _controller.Update(2, dto, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(updated, ok.Value);
        }

        // ── UpdateStatus ────────────────────────────────────────────────

        [Fact]
        public async Task UpdateStatus_WhenSuccessful_ReturnsOkWithUpdated()
        {
            // Arrange
            var statusDto = new UpdateShipmentStatusDto { Status = "Delivered" };
            var updated   = new FiberShipmentDto { Id = 5, Status = "Delivered" };
            _serviceMock.Setup(s => s.UpdateStatusAsync(5, statusDto, It.IsAny<CancellationToken>())).ReturnsAsync(updated);

            // Act
            var result = await _controller.UpdateStatus(5, statusDto, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(updated, ok.Value);
        }

        // ── Delete ──────────────────────────────────────────────────────

        [Fact]
        public async Task Delete_WhenDeleted_ReturnsNoContent()
        {
            // Arrange
            _serviceMock.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            var result = await _controller.Delete(1, CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Delete_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            _serviceMock.Setup(s => s.DeleteAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            var result = await _controller.Delete(99, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
