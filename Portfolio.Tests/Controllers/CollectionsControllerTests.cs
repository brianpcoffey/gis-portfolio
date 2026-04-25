using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;
using Xunit;

namespace Portfolio.Tests.Controllers
{
    public class CollectionsControllerTests
    {
        private readonly Mock<ICollectionService> _serviceMock;
        private readonly CollectionsController _controller;

        public CollectionsControllerTests()
        {
            _serviceMock = new Mock<ICollectionService>();
            _controller = new CollectionsController(
                _serviceMock.Object,
                new Mock<ILogger<CollectionsController>>().Object);
        }

        [Fact]
        public async Task GetAll_ReturnsOkWithItems()
        {
            // Arrange
            var items = new List<CollectionDto> { new() { Id = 1, Name = "Col1" } };
            _serviceMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(items);

            // Act
            var result = await _controller.GetAll(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var returned = Assert.IsAssignableFrom<IEnumerable<CollectionDto>>(ok.Value);
            Assert.Single(returned);
        }

        [Fact]
        public async Task Get_WhenFound_ReturnsOk()
        {
            // Arrange
            var dto = new CollectionDto { Id = 1, Name = "Col1" };
            _serviceMock.Setup(s => s.GetByIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(dto);

            // Act
            var result = await _controller.Get(1, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(dto, ok.Value);
        }

        [Fact]
        public async Task Get_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((CollectionDto?)null);

            // Act
            var result = await _controller.Get(99, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        [Fact]
        public async Task Create_WithValidDto_ReturnsCreated()
        {
            // Arrange
            var dto = new CollectionCreateDto { Name = "New Col" };
            var created = new CollectionDto { Id = 5, Name = "New Col" };
            _serviceMock.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>())).ReturnsAsync(created);

            // Act
            var result = await _controller.Create(dto, CancellationToken.None);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal(201, createdResult.StatusCode);
            Assert.Equal(created, createdResult.Value);
        }

        [Fact]
        public async Task Create_WhenArgumentException_ReturnsBadRequest()
        {
            // Arrange
            var dto = new CollectionCreateDto { Name = "" };
            _serviceMock.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Name required"));

            // Act
            var result = await _controller.Create(dto, CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.NotNull(bad.Value);
        }

        [Fact]
        public async Task Create_WhenConflict_ReturnsConflict()
        {
            // Arrange
            var dto = new CollectionCreateDto { Name = "Dup" };
            _serviceMock.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Duplicate"));

            // Act
            var result = await _controller.Create(dto, CancellationToken.None);

            // Assert
            Assert.IsType<ConflictObjectResult>(result.Result);
        }

        [Fact]
        public async Task Update_WhenFound_ReturnsOk()
        {
            // Arrange
            var dto = new CollectionUpdateDto { Name = "Updated" };
            var updated = new CollectionDto { Id = 1, Name = "Updated" };
            _serviceMock.Setup(s => s.UpdateAsync(1, dto, It.IsAny<CancellationToken>())).ReturnsAsync(updated);

            // Act
            var result = await _controller.Update(1, dto, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(updated, ok.Value);
        }

        [Fact]
        public async Task Update_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            var dto = new CollectionUpdateDto { Name = "X" };
            _serviceMock.Setup(s => s.UpdateAsync(99, dto, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException());

            // Act
            var result = await _controller.Update(99, dto, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

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
