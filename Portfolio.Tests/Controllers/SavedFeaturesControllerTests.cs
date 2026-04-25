using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Web.Controllers.Api;
using Portfolio.Services.Interfaces;
using Xunit;

namespace Portfolio.Tests.Controllers
{
    public class SavedFeaturesControllerTests
    {
        private readonly Mock<ISavedFeatureService> _serviceMock;
        private readonly SavedFeaturesController _controller;

        public SavedFeaturesControllerTests()
        {
            _serviceMock = new Mock<ISavedFeatureService>();
            _controller = new SavedFeaturesController(
                _serviceMock.Object,
                new Mock<ILogger<SavedFeaturesController>>().Object);
        }

        [Fact]
        public async Task GetAll_ReturnsOkWithList()
        {
            // Arrange
            var items = new List<SavedFeatureDto> { new() { Id = 1, Name = "Feature1" } };
            _serviceMock.Setup(s => s.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(items);

            // Act
            var result = await _controller.GetAll(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(items, ok.Value);
        }

        [Fact]
        public async Task Create_WithValidDto_ReturnsOk()
        {
            // Arrange
            var dto = new CreateSavedFeatureDto { LayerId = "1", FeatureId = "100", Name = "F", GeometryJson = "{}" };
            var created = new SavedFeatureDto { Id = 1, Name = "F" };
            _serviceMock.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>())).ReturnsAsync(created);

            // Act
            var result = await _controller.Create(dto, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(created, ok.Value);
        }

        [Theory]
        [InlineData("", "100")]
        [InlineData("1", "")]
        public async Task Create_WithMissingIds_ReturnsBadRequest(string layerId, string featureId)
        {
            // Arrange
            var dto = new CreateSavedFeatureDto { LayerId = layerId, FeatureId = featureId };

            // Act
            var result = await _controller.Create(dto, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task Create_WhenDuplicate_ReturnsConflict()
        {
            // Arrange
            var dto = new CreateSavedFeatureDto { LayerId = "1", FeatureId = "100", Name = "F", GeometryJson = "{}" };
            _serviceMock.Setup(s => s.CreateAsync(dto, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Feature already saved"));

            // Act
            var result = await _controller.Create(dto, CancellationToken.None);

            // Assert
            Assert.IsType<ConflictObjectResult>(result);
        }

        [Fact]
        public async Task Delete_WithNumericId_WhenFound_ReturnsNoContent()
        {
            // Arrange
            _serviceMock.Setup(s => s.DeleteByDbIdAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            var result = await _controller.Delete("1", CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Delete_WithNumericId_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            _serviceMock.Setup(s => s.DeleteByDbIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            var result = await _controller.Delete("99", CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task Delete_WithFeatureKey_WhenFound_ReturnsNoContent()
        {
            // Arrange
            _serviceMock.Setup(s => s.DeleteByFeatureKeyAsync("key-abc", It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            var result = await _controller.Delete("key-abc", CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Delete_WithFeatureKey_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            _serviceMock.Setup(s => s.DeleteByFeatureKeyAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            var result = await _controller.Delete("missing", CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public async Task Delete_WithBlankId_ReturnsBadRequest(string id)
        {
            // Act
            var result = await _controller.Delete(id, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }
    }
}
