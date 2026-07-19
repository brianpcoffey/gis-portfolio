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
    public class FiberOrdersControllerTests
    {
        private readonly Mock<IFiberOrderService> _serviceMock;
        private readonly FiberOrdersController _controller;

        public FiberOrdersControllerTests()
        {
            _serviceMock = new Mock<IFiberOrderService>();
            _controller  = new FiberOrdersController(
                _serviceMock.Object,
                new Mock<ILogger<FiberOrdersController>>().Object);
        }

        // ── GetAll ──────────────────────────────────────────────────────

        [Fact]
        public async Task GetAll_ReturnsOkWithList()
        {
            // Arrange
            var orders = new List<FiberOrderDto> { new() { Id = 1, ClientName = "Acme" } };
            _serviceMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(orders);

            // Act
            var result = await _controller.GetAll(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(orders, ok.Value);
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
            var dto = new FiberOrderDto { Id = 3, ClientName = "Beta Corp" };
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
            _serviceMock.Setup(s => s.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((FiberOrderDto?)null);

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
        public async Task Create_WithValidDto_ReturnsOkWithCreatedOrder()
        {
            // Arrange
            var dto     = new CreateFiberOrderDto { ClientName = "Gamma Inc", ProductName = "Cable", Quantity = 5, UnitPrice = 10m };
            var created = new FiberOrderDto { Id = 7, ClientName = "Gamma Inc" };
            _serviceMock.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>())).ReturnsAsync(created);

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
            var dto = new CreateFiberOrderDto { ClientName = "X" };
            _serviceMock.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new Exception("db error"));

            // Act
            var result = await _controller.Create(dto, CancellationToken.None);

            // Assert
            var status = Assert.IsType<ObjectResult>(result);
            Assert.Equal(StatusCodes.Status500InternalServerError, status.StatusCode);
        }

        // ── Update ──────────────────────────────────────────────────────

        [Fact]
        public async Task Update_WhenSuccessful_ReturnsOkWithUpdatedOrder()
        {
            // Arrange
            var dto     = new UpdateFiberOrderDto { ClientName = "Updated" };
            var updated = new FiberOrderDto { Id = 2, ClientName = "Updated" };
            _serviceMock.Setup(s => s.UpdateAsync(2, dto, It.IsAny<CancellationToken>())).ReturnsAsync(updated);

            // Act
            var result = await _controller.Update(2, dto, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(updated, ok.Value);
        }

        [Fact]
        public async Task Update_WhenServiceThrows_Returns500ProblemDetails()
        {
            // Arrange
            var dto = new UpdateFiberOrderDto { ClientName = "X" };
            _serviceMock.Setup(s => s.UpdateAsync(1, dto, It.IsAny<CancellationToken>()))
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
    }
}
