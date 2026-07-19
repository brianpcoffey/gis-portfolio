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
    public class FiberMaterialsControllerTests
    {
        private readonly Mock<IFiberMaterialService> _serviceMock;
        private readonly FiberMaterialsController _controller;

        public FiberMaterialsControllerTests()
        {
            _serviceMock = new Mock<IFiberMaterialService>();
            _controller  = new FiberMaterialsController(
                _serviceMock.Object,
                new Mock<ILogger<FiberMaterialsController>>().Object);
        }

        // ── GetAll ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetAll_ReturnsOkWithList()
        {
            // Arrange
            var materials = new List<FiberMaterialDto> { new() { Id = 1, Name = "Single Mode Fiber" } };
            _serviceMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(materials);

            // Act
            var result = await _controller.GetAll(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(materials, ok.Value);
        }

        [Fact]
        public async Task GetAll_WhenServiceThrows_Returns500ProblemDetails()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("db error"));

            // Act
            var result = await _controller.GetAll(CancellationToken.None);

            // Assert
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
            var problem = Assert.IsType<ProblemDetails>(status.Value);
            Assert.Equal(500, problem.Status);
        }

        // ── Get ─────────────────────────────────────────────────────────

        [Fact]
        public async Task Get_WhenFound_ReturnsOk()
        {
            // Arrange
            var dto = new FiberMaterialDto { Id = 2, Name = "Multimode Fiber" };
            _serviceMock.Setup(s => s.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

            // Act
            var result = await _controller.Get(2, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(dto, ok.Value);
        }

        [Fact]
        public async Task Get_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((FiberMaterialDto?)null);

            // Act
            var result = await _controller.Get(99, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Get_WhenServiceThrows_Returns500ProblemDetails()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("unexpected"));

            // Act
            var result = await _controller.Get(1, CancellationToken.None);

            // Assert
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
        }

        // ── Create ──────────────────────────────────────────────────────

        [Fact]
        public async Task Create_WithValidDto_ReturnsOkWithCreated()
        {
            // Arrange
            var dto     = new CreateFiberMaterialDto { Name = "Ribbon Cable", Sku = "RC001", Category = "Cable" };
            var created = new FiberMaterialDto { Id = 5, Name = "Ribbon Cable" };
            _serviceMock.Setup(s => s.CreateAsync(It.IsAny<FiberMaterialDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(created);

            // Act
            var result = await _controller.Create(dto, CancellationToken.None);

            // Assert — 201 Created pointing at the Get action.
            var createdResult = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(created, createdResult.Value);
        }

        [Fact]
        public async Task Create_WhenServiceThrows_Returns500ProblemDetails()
        {
            // Arrange
            var dto = new CreateFiberMaterialDto { Name = "X" };
            _serviceMock.Setup(s => s.CreateAsync(It.IsAny<FiberMaterialDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("db error"));

            // Act
            var result = await _controller.Create(dto, CancellationToken.None);

            // Assert
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
        }

        // ── Update ──────────────────────────────────────────────────────

        [Fact]
        public async Task Update_WhenSuccessful_ReturnsOkWithUpdated()
        {
            // Arrange
            var dto     = new UpdateFiberMaterialDto { Name = "Updated Fiber" };
            var updated = new FiberMaterialDto { Id = 3, Name = "Updated Fiber" };
            _serviceMock.Setup(s => s.UpdateAsync(3, It.IsAny<FiberMaterialDto>(), It.IsAny<CancellationToken>())).ReturnsAsync(updated);

            // Act
            var result = await _controller.Update(3, dto, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(updated, ok.Value);
        }

        [Fact]
        public async Task Update_WhenServiceThrows_Returns500ProblemDetails()
        {
            // Arrange
            var dto = new UpdateFiberMaterialDto { Name = "X" };
            _serviceMock.Setup(s => s.UpdateAsync(1, It.IsAny<FiberMaterialDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("unexpected"));

            // Act
            var result = await _controller.Update(1, dto, CancellationToken.None);

            // Assert
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
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

        [Fact]
        public async Task Delete_WhenServiceThrows_Returns500ProblemDetails()
        {
            // Arrange
            _serviceMock.Setup(s => s.DeleteAsync(1, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("unexpected"));

            // Act
            var result = await _controller.Delete(1, CancellationToken.None);

            // Assert
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
        }

        // ── ReceiveStock ────────────────────────────────────────────────

        [Fact]
        public async Task ReceiveStock_WhenSuccessful_ReturnsOkWithUpdatedMaterial()
        {
            // Arrange
            var dto     = new ReceiveStockDto { Quantity = 100m, Notes = "Shipment A" };
            var updated = new FiberMaterialDto { Id = 4, QtyOnHand = 250m };
            _serviceMock.Setup(s => s.ReceiveStockAsync(4, dto, It.IsAny<CancellationToken>())).ReturnsAsync(updated);

            // Act
            var result = await _controller.ReceiveStock(4, dto, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(updated, ok.Value);
        }

        [Fact]
        public async Task ReceiveStock_WhenServiceThrows_Returns500ProblemDetails()
        {
            // Arrange
            var dto = new ReceiveStockDto { Quantity = 10m };
            _serviceMock.Setup(s => s.ReceiveStockAsync(1, dto, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("unexpected"));

            // Act
            var result = await _controller.ReceiveStock(1, dto, CancellationToken.None);

            // Assert
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
        }
    }
}
