using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Services;
using Xunit;

namespace Portfolio.Tests.Services
{
    public class HomeScoringServiceTests
    {
        private readonly Mock<IPropertyRepository> _propertyRepoMock;
        private readonly HomeScoringService _service;

        public HomeScoringServiceTests()
        {
            _propertyRepoMock = new Mock<IPropertyRepository>();
            _service = new HomeScoringService(_propertyRepoMock.Object);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_ReturnsScoredProperties()
        {
            var prefs = new HomeSearchPreferencesDto { MaxPrice = 1000000, MinBedrooms = 2, MinBathrooms = 2, MinSqft = 1000, MaxCommuteMin = 60 };
            _propertyRepoMock.Setup(r => r.GetFilteredAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Property> { new Property { Id = 1 }, new Property { Id = 2 } });
            var result = await _service.GetTopPropertiesAsync(prefs);
            Assert.Equal(2, result.Count);
        }

        [Fact]
        public async Task GetTopPropertiesAsync_WhenNoProperties_ReturnsEmptyList()
        {
            var prefs = new HomeSearchPreferencesDto { MaxPrice = 1000000, MinBedrooms = 2, MinBathrooms = 2, MinSqft = 1000, MaxCommuteMin = 60 };
            _propertyRepoMock.Setup(r => r.GetFilteredAsync(It.IsAny<decimal>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Property>());
            var result = await _service.GetTopPropertiesAsync(prefs);
            Assert.Empty(result);
        }

        // ── GetPropertyByIdAsync ────────────────────────────────────────

        [Fact]
        public async Task GetPropertyByIdAsync_WhenFound_ReturnsScoredPropertyDto()
        {
            // Arrange
            var property = new Property { Id = 42, Street = "123 Main St", City = "Denver", ZipCode = "80203" };
            _propertyRepoMock
                .Setup(r => r.GetByIdAsync(42, It.IsAny<CancellationToken>()))
                .ReturnsAsync(property);

            // Act
            var result = await _service.GetPropertyByIdAsync(42);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(42, result.PropertyId);
            Assert.Equal("123 Main St", result.Street);
            Assert.Equal("Denver", result.City);
        }

        [Fact]
        public async Task GetPropertyByIdAsync_WhenNotFound_ReturnsNull()
        {
            // Arrange
            _propertyRepoMock
                .Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Property?)null);

            // Act
            var result = await _service.GetPropertyByIdAsync(999);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetPropertyByIdAsync_PassesCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var token = cts.Token;
            _propertyRepoMock
                .Setup(r => r.GetByIdAsync(1, token))
                .ReturnsAsync((Property?)null);

            // Act
            await _service.GetPropertyByIdAsync(1, token);

            // Assert
            _propertyRepoMock.Verify(r => r.GetByIdAsync(1, token), Times.Once);
        }
    }
}
