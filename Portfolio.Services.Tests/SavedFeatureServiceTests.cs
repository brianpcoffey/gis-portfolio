using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Services;
using Xunit;

namespace Portfolio.Services.Tests
{
    public class SavedFeatureServiceTests
    {
        #region GetAllAsync

        [Fact]
        public async Task GetAllAsync_ReturnsMappedDtos()
        {
            // Arrange
            var mockRepo = new Mock<ISavedFeatureRepository>();
            mockRepo.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<SavedFeature>
            {
                new SavedFeature
                {
                    Id = 1,
                    LayerId = "3",
                    FeatureId = "100",
                    Name = "Test Feature",
                    GeometryJson = "{}",
                    DateSaved = DateTime.UtcNow
                }
            });
            var service = new SavedFeatureService(mockRepo.Object);

            // Act
            var result = await service.GetAllAsync();

            // Assert
            Assert.Single(result);
            Assert.Equal("Test Feature", result[0].Name);
        }

        #endregion

        #region AddAsync

        [Fact]
        public async Task AddAsync_MapsAndPersistsEntity()
        {
            // Arrange
            var dto = new SavedFeatureDto
            {
                LayerId = "3",
                FeatureId = "101",
                Name = "New Feature",
                GeometryJson = "{}"
            };
            var mockRepo = new Mock<ISavedFeatureRepository>();
            mockRepo.Setup(r => r.AddAsync(It.IsAny<SavedFeature>()))
                .ReturnsAsync((SavedFeature f) => { f.Id = 2; return f; });
            var service = new SavedFeatureService(mockRepo.Object);

            // Act
            var result = await service.AddAsync(dto);

            // Assert
            Assert.Equal("New Feature", result.Name);
            Assert.Equal(2, result.Id);
        }

        #endregion

        #region DeleteAsync

        [Fact]
        public async Task DeleteAsync_DeletesAndReturnsTrue()
        {
            // Arrange
            var mockRepo = new Mock<ISavedFeatureRepository>();
            mockRepo.Setup(r => r.DeleteAsync(1)).ReturnsAsync(true);
            var service = new SavedFeatureService(mockRepo.Object);

            // Act
            var result = await service.DeleteAsync(1);

            // Assert
            Assert.True(result);
        }

        #endregion
    }
}