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
    }
}
