using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;
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

        [Fact]
        public async Task Create_MissingIds_PinsExactErrorBody()
        {
            // Act
            var result = await _controller.Create(
                new CreateSavedFeatureDto { LayerId = "", FeatureId = "f" }, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, "LayerId and FeatureId are required.");
        }

        [Fact]
        public async Task Create_ArgumentException_Propagates()
        {
            // Arrange: [BadRequestOnArgumentException] turns it into the 400 body (see the filter tests).
            _serviceMock.Setup(s => s.CreateAsync(It.IsAny<CreateSavedFeatureDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ArgumentException("Unknown layer.", "dto"));

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _controller.Create(
                new CreateSavedFeatureDto { LayerId = "l", FeatureId = "f" }, CancellationToken.None));

            // Assert: the exception propagates from the action unchanged. The filter shows its message only
            // when Portfolio code threw it; an exception handed back by a mock is rethrown by .NET, so it gets the generic text.
            Assert.Equal("Unknown layer. (Parameter 'dto')", ex.Message);
        }

        [Fact]
        public async Task Create_InvalidOperation_PinsFixedConflictBody()
        {
            // Arrange
            // The service message is discarded: the body is always this fixed text.
            _serviceMock.Setup(s => s.CreateAsync(It.IsAny<CreateSavedFeatureDto>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("internal detail that must not leak"));

            // Act
            var result = await _controller.Create(
                new CreateSavedFeatureDto { LayerId = "l", FeatureId = "f" }, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 409, "Feature already saved");
        }
    }
}
