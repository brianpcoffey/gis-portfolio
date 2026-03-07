using Moq;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    public class SavedSearchServiceTests
    {
        private readonly Mock<ISavedSearchRepository> _repoMock;
        private readonly SavedSearchService _service;
        private readonly Guid _testUserId = Guid.NewGuid();

        public SavedSearchServiceTests()
        {
            _repoMock = new Mock<ISavedSearchRepository>();
            _service = new SavedSearchService(_repoMock.Object);
        }

        // ── CreateSavedSearchAsync ──────────────────────────────────────

        [Fact]
        public async Task CreateSavedSearchAsync_WithValidInput_ReturnsCreatedEntity()
        {
            // Arrange
            var search = new SavedSearch
            {
                UserId = _testUserId,
                Name = "My Search",
                PreferencesJson = "{}",
                TopPropertyIds = "1,2,3"
            };

            _repoMock
                .Setup(r => r.ExistsByNameAsync(_testUserId, "My Search", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            _repoMock
                .Setup(r => r.AddAsync(It.IsAny<SavedSearch>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SavedSearch s, CancellationToken _) =>
                {
                    s.Id = 1;
                    return s;
                });

            // Act
            var result = await _service.CreateSavedSearchAsync(search);

            // Assert
            Assert.Equal(1, result.Id);
            Assert.Equal("My Search", result.Name);
            Assert.Equal(_testUserId, result.UserId);
            Assert.True(result.CreatedAt > DateTime.MinValue);
            _repoMock.Verify(r => r.AddAsync(It.IsAny<SavedSearch>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateSavedSearchAsync_SetsCreatedAtToUtcNow()
        {
            // Arrange
            var before = DateTime.UtcNow;
            var search = new SavedSearch
            {
                UserId = _testUserId,
                Name = "Timestamped Search",
                PreferencesJson = "{}"
            };

            _repoMock
                .Setup(r => r.ExistsByNameAsync(_testUserId, "Timestamped Search", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _repoMock
                .Setup(r => r.AddAsync(It.IsAny<SavedSearch>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SavedSearch s, CancellationToken _) => s);

            // Act
            var result = await _service.CreateSavedSearchAsync(search);
            var after = DateTime.UtcNow;

            // Assert
            Assert.InRange(result.CreatedAt, before, after);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateSavedSearchAsync_WithEmptyOrNullName_ThrowsArgumentException(string? name)
        {
            // Arrange
            var search = new SavedSearch
            {
                UserId = _testUserId,
                Name = name!,
                PreferencesJson = "{}"
            };

            // Act & Assert
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CreateSavedSearchAsync(search));
            Assert.Contains("Name is required", ex.Message);
            _repoMock.Verify(r => r.AddAsync(It.IsAny<SavedSearch>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateSavedSearchAsync_WithDuplicateName_ThrowsInvalidOperationException()
        {
            // Arrange
            var search = new SavedSearch
            {
                UserId = _testUserId,
                Name = "Duplicate",
                PreferencesJson = "{}"
            };

            _repoMock
                .Setup(r => r.ExistsByNameAsync(_testUserId, "Duplicate", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.CreateSavedSearchAsync(search));
            Assert.Contains("already exists", ex.Message);
            _repoMock.Verify(r => r.AddAsync(It.IsAny<SavedSearch>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CreateSavedSearchAsync_PassesCancellationToken()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var token = cts.Token;
            var search = new SavedSearch
            {
                UserId = _testUserId,
                Name = "Token Test",
                PreferencesJson = "{}"
            };

            _repoMock
                .Setup(r => r.ExistsByNameAsync(_testUserId, "Token Test", token))
                .ReturnsAsync(false);
            _repoMock
                .Setup(r => r.AddAsync(It.IsAny<SavedSearch>(), token))
                .ReturnsAsync((SavedSearch s, CancellationToken _) => s);

            // Act
            await _service.CreateSavedSearchAsync(search, token);

            // Assert
            _repoMock.Verify(r => r.ExistsByNameAsync(_testUserId, "Token Test", token), Times.Once);
            _repoMock.Verify(r => r.AddAsync(It.IsAny<SavedSearch>(), token), Times.Once);
        }

        // ── GetSavedSearchesAsync ───────────────────────────────────────

        [Fact]
        public async Task GetSavedSearchesAsync_ReturnsListForUser()
        {
            // Arrange
            var searches = new List<SavedSearch>
            {
                new() { Id = 1, UserId = _testUserId, Name = "Search A", PreferencesJson = "{}", CreatedAt = DateTime.UtcNow },
                new() { Id = 2, UserId = _testUserId, Name = "Search B", PreferencesJson = "{}", CreatedAt = DateTime.UtcNow }
            };

            _repoMock
                .Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(searches);

            // Act
            var result = await _service.GetSavedSearchesAsync(_testUserId);

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Equal("Search A", result[0].Name);
            Assert.Equal("Search B", result[1].Name);
        }

        [Fact]
        public async Task GetSavedSearchesAsync_WhenNone_ReturnsEmptyList()
        {
            // Arrange
            _repoMock
                .Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SavedSearch>());

            // Act
            var result = await _service.GetSavedSearchesAsync(_testUserId);

            // Assert
            Assert.Empty(result);
        }

        [Fact]
        public async Task GetSavedSearchesAsync_DoesNotReturnOtherUsersSearches()
        {
            // Arrange
            var otherUserId = Guid.NewGuid();
            _repoMock
                .Setup(r => r.GetAllAsync(otherUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<SavedSearch>());

            // Act
            var result = await _service.GetSavedSearchesAsync(otherUserId);

            // Assert
            Assert.Empty(result);
            _repoMock.Verify(r => r.GetAllAsync(otherUserId, It.IsAny<CancellationToken>()), Times.Once);
        }

        // ── DeleteSavedSearchAsync ──────────────────────────────────────

        [Fact]
        public async Task DeleteSavedSearchAsync_WhenExists_DeletesSuccessfully()
        {
            // Arrange
            var existing = new SavedSearch
            {
                Id = 1,
                UserId = _testUserId,
                Name = "To Delete",
                PreferencesJson = "{}",
                CreatedAt = DateTime.UtcNow
            };

            _repoMock
                .Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            _repoMock
                .Setup(r => r.DeleteAsync(1, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            await _service.DeleteSavedSearchAsync(1);

            // Assert
            _repoMock.Verify(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()), Times.Once);
            _repoMock.Verify(r => r.DeleteAsync(1, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteSavedSearchAsync_WhenNotFound_ThrowsKeyNotFoundException()
        {
            // Arrange
            _repoMock
                .Setup(r => r.GetByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((SavedSearch?)null);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _service.DeleteSavedSearchAsync(999));
            Assert.Contains("999", ex.Message);
            _repoMock.Verify(r => r.DeleteAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}