using Moq;
using Portfolio.Common.Models;
using Portfolio.Common.DTOs;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;
using Xunit;

namespace Portfolio.Tests.Services
{
    public class SavedFeatureServiceTests
    {
        private readonly Mock<ISavedFeatureRepository> _repositoryMock;
        private readonly Mock<IUserNoteRepository> _noteRepositoryMock;
        private readonly Mock<IUserProfileService> _userProfileServiceMock;
        private readonly SavedFeatureService _service;
        private readonly Guid _testUserId = Guid.NewGuid();

        public SavedFeatureServiceTests()
        {
            _repositoryMock = new Mock<ISavedFeatureRepository>();
            _noteRepositoryMock = new Mock<IUserNoteRepository>();
            _userProfileServiceMock = new Mock<IUserProfileService>();

            _userProfileServiceMock.Setup(s => s.GetCurrentUserId()).Returns(_testUserId);

            _service = new SavedFeatureService(
                _repositoryMock.Object,
                _noteRepositoryMock.Object,
                _userProfileServiceMock.Object);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllFeatures()
        {
            // Arrange
            var features = new List<SavedFeature>
            {
                new() { Id = 1, UserId = _testUserId, LayerId = "1", FeatureId = "100", Name = "Test Feature" }
            };
            _repositoryMock.Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(features);

            // Act
            var result = await _service.GetAllAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal("Test Feature", result[0].Name);
        }

        [Fact]
        public async Task CreateAsync_WhenFeatureExists_ThrowsInvalidOperationException()
        {
            // Arrange
            var dto = new CreateSavedFeatureDto
            {
                LayerId = "1",
                FeatureId = "100",
                Name = "Test",
                GeometryJson = "{}"
            };
            _repositoryMock.Setup(r => r.GetByLayerAndFeatureIdAsync("1", "100", _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SavedFeature { Id = 1 });

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateAsync(dto));
        }

        [Fact]
        public async Task CreateAsync_WithValidData_ReturnsCreatedFeature()
        {
            // Arrange
            var dto = new CreateSavedFeatureDto
            {
                LayerId = "1",
                FeatureId = "100",
                Name = "Test",
                GeometryJson = "{}"
            };
            _repositoryMock.Setup(r => r.GetByLayerAndFeatureIdAsync("1", "100", _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SavedFeature?)null);
            _repositoryMock.Setup(r => r.AddAsync(It.IsAny<SavedFeature>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SavedFeature f, CancellationToken _) => { f.Id = 1; return f; });

            // Act
            var result = await _service.CreateAsync(dto);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.Equal("Test", result.Name);
        }

        [Theory]
        [InlineData("", "100", "Name", "{}")]
        [InlineData("1", "", "Name", "{}")]
        [InlineData("1", "100", "", "{}")]
        [InlineData("1", "100", "Name", "")]
        public async Task CreateAsync_WithMissingFields_ThrowsArgumentException(
            string layerId, string featureId, string name, string geometryJson)
        {
            // Arrange
            var dto = new CreateSavedFeatureDto
            {
                LayerId = layerId,
                FeatureId = featureId,
                Name = name,
                GeometryJson = geometryJson
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
        }
    }
}