using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    public class SavedFeatureServiceTests
    {
        private readonly Mock<ISavedFeatureRepository> _repoMock;
        private readonly Mock<IUserProfileService> _userProfileServiceMock;
        private readonly SavedFeatureService _service;
        private readonly Guid _testUserId = Guid.NewGuid();

        public SavedFeatureServiceTests()
        {
            _repoMock = new Mock<ISavedFeatureRepository>();
            _userProfileServiceMock = new Mock<IUserProfileService>();
            _userProfileServiceMock.Setup(x => x.GetCurrentUserId()).Returns(_testUserId);

            _service = new SavedFeatureService(_repoMock.Object, _userProfileServiceMock.Object, TimeProvider.System);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsMappedDtos()
        {
            var features = new List<SavedFeature>
            {
                new()
                {
                    Id = 1, UserId = _testUserId, LayerId = "3", FeatureId = "42",
                    Name = "Colorado", GeometryJson = "{}", DateSaved = DateTime.UtcNow
                },
                new()
                {
                    Id = 2, UserId = _testUserId, LayerId = "3", FeatureId = "43",
                    Name = "Texas", GeometryJson = "{}", DateSaved = DateTime.UtcNow
                }
            };
            _repoMock
                .Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(features);

            var result = await _service.GetAllAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal("Colorado", result[0].Name);
            Assert.Equal("Texas", result[1].Name);
        }

        [Fact]
        public async Task GetAllAsync_WhenNoUser_ThrowsInvalidOperationException()
        {
            _userProfileServiceMock.Setup(x => x.GetCurrentUserId()).Returns((Guid?)null);
            var service = new SavedFeatureService(_repoMock.Object, _userProfileServiceMock.Object, TimeProvider.System);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetAllAsync());
        }

        [Fact]
        public async Task CreateAsync_WithValidDto_ReturnsDto()
        {
            var dto = new CreateSavedFeatureDto
            {
                LayerId = "3",
                FeatureId = "42",
                Name = "Colorado",
                GeometryJson = "{}",
                Description = "A state"
            };
            _repoMock
                .Setup(r => r.GetByLayerAndFeatureIdAsync("3", "42", _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SavedFeature?)null);
            _repoMock
                .Setup(r => r.AddAsync(It.IsAny<SavedFeature>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SavedFeature f, CancellationToken _) => { f.Id = 1; return f; });

            var result = await _service.CreateAsync(dto);

            Assert.Equal(1, result.Id);
            Assert.Equal("Colorado", result.Name);
            Assert.Equal("3", result.LayerId);
        }

        [Fact]
        public async Task CreateAsync_WhenDuplicate_ThrowsInvalidOperationException()
        {
            var dto = new CreateSavedFeatureDto
            {
                LayerId = "3",
                FeatureId = "42",
                Name = "Colorado",
                GeometryJson = "{}"
            };
            _repoMock
                .Setup(r => r.GetByLayerAndFeatureIdAsync("3", "42", _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new SavedFeature { Id = 1 });

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(dto));
        }

        [Theory]
        [InlineData("", "42", "Name", "{}")]
        [InlineData("3", "", "Name", "{}")]
        [InlineData("3", "42", "", "{}")]
        [InlineData("3", "42", "Name", "")]
        public async Task CreateAsync_WithMissingRequiredFields_ThrowsArgumentException(
            string layerId, string featureId, string name, string geometryJson)
        {
            var dto = new CreateSavedFeatureDto
            {
                LayerId = layerId,
                FeatureId = featureId,
                Name = name,
                GeometryJson = geometryJson
            };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
        }

        [Fact]
        public async Task DeleteByDbIdAsync_WhenExists_ReturnsTrue()
        {
            _repoMock
                .Setup(r => r.DeleteAsync(1, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await _service.DeleteByDbIdAsync(1);

            Assert.True(result);
        }

        [Fact]
        public async Task DeleteByDbIdAsync_WhenNotExists_ReturnsFalse()
        {
            _repoMock
                .Setup(r => r.DeleteAsync(999, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            var result = await _service.DeleteByDbIdAsync(999);

            Assert.False(result);
        }

        [Fact]
        public async Task DeleteByFeatureKeyAsync_WhenExists_ReturnsTrue()
        {
            var feature = new SavedFeature { Id = 5, UserId = _testUserId };
            _repoMock
                .Setup(r => r.GetByFeatureKeyAsync("3-42", _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(feature);
            _repoMock
                .Setup(r => r.DeleteAsync(5, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            var result = await _service.DeleteByFeatureKeyAsync("3-42");

            Assert.True(result);
        }

        [Fact]
        public async Task DeleteByFeatureKeyAsync_WhenNotFound_ReturnsFalse()
        {
            _repoMock
                .Setup(r => r.GetByFeatureKeyAsync("none", _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SavedFeature?)null);

            var result = await _service.DeleteByFeatureKeyAsync("none");

            Assert.False(result);
        }
    }
}