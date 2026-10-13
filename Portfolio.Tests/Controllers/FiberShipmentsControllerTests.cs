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
                _serviceMock.Object);
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

        // ── Error paths: FiberShipmentsController has no catch clauses, so invalid model
        //    state returns a 400 validation problem and a service exception propagates. ──

        private static void AssertValidationProblem(IActionResult result)
        {
            var bad = Assert.IsType<ObjectResult>(result);
            // With no ProblemDetailsFactory (a bare controller) neither the result nor the problem
            // carries a status; the 400 is applied at runtime by the [ApiController] factory.
            Assert.Null(bad.StatusCode);
            var problem = Assert.IsType<ValidationProblemDetails>(bad.Value);
            Assert.Null(problem.Status);
            Assert.Equal(["Name is required."], problem.Errors["Name"]);
        }

        [Fact]
        public async Task Create_InvalidModelState_ReturnsValidationProblemWithoutCallingService()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Name is required.");

            // Act
            var result = await _controller.Create(new FiberShipmentDto(), CancellationToken.None);

            // Assert
            AssertValidationProblem(result);
            _serviceMock.Verify(s => s.CreateAsync(It.IsAny<FiberShipmentDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Update_InvalidModelState_ReturnsValidationProblemWithoutCallingService()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Name is required.");

            // Act
            var result = await _controller.Update(1, new FiberShipmentDto(), CancellationToken.None);

            // Assert
            AssertValidationProblem(result);
            _serviceMock.Verify(s => s.UpdateAsync(It.IsAny<int>(), It.IsAny<FiberShipmentDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateStatus_InvalidModelState_ReturnsValidationProblemWithoutCallingService()
        {
            // Arrange
            _controller.ModelState.AddModelError("Name", "Name is required.");

            // Act
            var result = await _controller.UpdateStatus(1, new UpdateShipmentStatusDto(), CancellationToken.None);

            // Assert
            AssertValidationProblem(result);
            _serviceMock.Verify(s => s.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<UpdateShipmentStatusDto>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Create_ServiceThrows_ExceptionPropagatesToTheCaller()
        {
            // Arrange
            _serviceMock.Setup(s => s.CreateAsync(It.IsAny<FiberShipmentDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("boom"));

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _controller.Create(new FiberShipmentDto(), CancellationToken.None));
        }

        [Fact]
        public async Task Update_ServiceThrows_ExceptionPropagatesToTheCaller()
        {
            // Arrange
            _serviceMock.Setup(s => s.UpdateAsync(It.IsAny<int>(), It.IsAny<FiberShipmentDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("missing"));

            // Act & Assert
            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _controller.Update(1, new FiberShipmentDto(), CancellationToken.None));
        }

        [Fact]
        public async Task UpdateStatus_ServiceThrows_ExceptionPropagatesToTheCaller()
        {
            // Arrange
            _serviceMock.Setup(s => s.UpdateStatusAsync(It.IsAny<int>(), It.IsAny<UpdateShipmentStatusDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("bad status"));

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _controller.UpdateStatus(1, new UpdateShipmentStatusDto(), CancellationToken.None));
        }
    }
}
