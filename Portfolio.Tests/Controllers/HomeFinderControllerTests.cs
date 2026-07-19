using Microsoft.AspNetCore.Mvc;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;
using Xunit;

namespace Portfolio.Tests.Controllers
{
    public class HomeFinderControllerTests
    {
        private readonly Mock<IHomeScoringService> _scoringMock;
        private readonly Mock<IUserProfileService> _profileMock;
        private readonly Mock<ISavedSearchService> _savedSearchMock;
        private readonly HomeFinderController _controller;
        private readonly Guid _testUserId = Guid.NewGuid();

        public HomeFinderControllerTests()
        {
            _scoringMock     = new Mock<IHomeScoringService>();
            _profileMock     = new Mock<IUserProfileService>();
            _savedSearchMock = new Mock<ISavedSearchService>();
            _controller = new HomeFinderController(
                _scoringMock.Object,
                _profileMock.Object,
                _savedSearchMock.Object);
        }

        // ── Search ──────────────────────────────────────────────────────

        [Fact]
        public async Task Search_WithValidPrefs_ReturnsOkWithResults()
        {
            // Arrange
            var prefs = new HomeSearchPreferencesDto { MaxPrice = 500000, MinBedrooms = 2 };
            var scored = new List<ScoredPropertyDto> { new() { PropertyId = 1, CompositeScore = 0.9 } };
            _scoringMock
                .Setup(s => s.GetTopPropertiesAsync(prefs, 10, It.IsAny<CancellationToken>()))
                .ReturnsAsync(scored);

            // Act
            var result = await _controller.Search(prefs, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(scored, ok.Value);
        }

        // ── GetProperty ─────────────────────────────────────────────────

        [Fact]
        public async Task GetProperty_WhenFound_ReturnsOk()
        {
            // Arrange
            var dto = new ScoredPropertyDto { PropertyId = 5, Street = "10 Elm St" };
            _scoringMock
                .Setup(s => s.GetPropertyByIdAsync(5, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.GetProperty(5, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(dto, ok.Value);
        }

        [Fact]
        public async Task GetProperty_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            _scoringMock
                .Setup(s => s.GetPropertyByIdAsync(999, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ScoredPropertyDto?)null);

            // Act
            var result = await _controller.GetProperty(999, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // ── SaveSearch ──────────────────────────────────────────────────

        [Fact]
        public async Task SaveSearch_WithValidRequest_ReturnsCreated()
        {
            // Arrange
            _profileMock.Setup(p => p.GetCurrentUserId()).Returns(_testUserId);
            var request = new SaveSearchRequest { Name = "My Search", Preferences = new HomeSearchPreferencesDto() };
            var saved = new SavedSearchDto { Id = 1, Name = "My Search", CreatedAt = DateTime.UtcNow };
            _savedSearchMock
                .Setup(s => s.CreateSavedSearchAsync(It.IsAny<CreateSavedSearchDto>(), _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(saved);

            // Act
            var result = await _controller.SaveSearch(request, CancellationToken.None);

            // Assert
            var created = Assert.IsType<CreatedAtActionResult>(result);
            Assert.Equal(saved, created.Value);
        }

        [Fact]
        public async Task SaveSearch_WhenNotAuthenticated_ReturnsUnauthorized()
        {
            // Arrange
            _profileMock.Setup(p => p.GetCurrentUserId()).Returns((Guid?)null);
            var request = new SaveSearchRequest { Name = "My Search" };

            // Act
            var result = await _controller.SaveSearch(request, CancellationToken.None);

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }

        [Fact]
        public async Task SaveSearch_WithDuplicateName_ReturnsConflict()
        {
            // Arrange
            _profileMock.Setup(p => p.GetCurrentUserId()).Returns(_testUserId);
            var request = new SaveSearchRequest { Name = "Duplicate" };
            _savedSearchMock
                .Setup(s => s.CreateSavedSearchAsync(It.IsAny<CreateSavedSearchDto>(), _testUserId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("already exists"));

            // Act
            var result = await _controller.SaveSearch(request, CancellationToken.None);

            // Assert
            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.NotNull(conflict.Value);
        }

        // ── GetSearches ─────────────────────────────────────────────────

        [Fact]
        public async Task GetSearches_WithAuthenticatedUser_ReturnsOkWithList()
        {
            // Arrange
            _profileMock.Setup(p => p.GetCurrentUserId()).Returns(_testUserId);
            var searches = new List<SavedSearchDto>
            {
                new() { Id = 1, Name = "A" },
                new() { Id = 2, Name = "B" }
            };
            _savedSearchMock
                .Setup(s => s.GetSavedSearchesAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(searches);

            // Act
            var result = await _controller.GetSearches(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var returned = Assert.IsAssignableFrom<List<SavedSearchDto>>(ok.Value);
            Assert.Equal(2, returned.Count);
        }

        [Fact]
        public async Task GetSearches_WhenNotAuthenticated_ReturnsUnauthorized()
        {
            // Arrange
            _profileMock.Setup(p => p.GetCurrentUserId()).Returns((Guid?)null);

            // Act
            var result = await _controller.GetSearches(CancellationToken.None);

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }

        // ── GetSearch ───────────────────────────────────────────────────

        [Fact]
        public async Task GetSearch_WhenFound_ReturnsOk()
        {
            // Arrange
            _profileMock.Setup(p => p.GetCurrentUserId()).Returns(_testUserId);
            var search = new SavedSearchDto { Id = 3, Name = "Found" };
            _savedSearchMock
                .Setup(s => s.GetSavedSearchesAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([search]);

            // Act
            var result = await _controller.GetSearch(3, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(search, ok.Value);
        }

        [Fact]
        public async Task GetSearch_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            _profileMock.Setup(p => p.GetCurrentUserId()).Returns(_testUserId);
            _savedSearchMock
                .Setup(s => s.GetSavedSearchesAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);

            // Act
            var result = await _controller.GetSearch(99, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // ── DeleteSearch ────────────────────────────────────────────────

        [Fact]
        public async Task DeleteSearch_WhenExists_ReturnsNoContent()
        {
            // Arrange
            _profileMock.Setup(p => p.GetCurrentUserId()).Returns(_testUserId);
            _savedSearchMock
                .Setup(s => s.DeleteSavedSearchAsync(1, _testUserId, It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.DeleteSearch(1, CancellationToken.None);

            // Assert — the delete is scoped to the current user's id.
            Assert.IsType<NoContentResult>(result);
            _savedSearchMock.Verify(s => s.DeleteSavedSearchAsync(1, _testUserId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteSearch_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            _profileMock.Setup(p => p.GetCurrentUserId()).Returns(_testUserId);
            _savedSearchMock
                .Setup(s => s.DeleteSavedSearchAsync(999, _testUserId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException("not found"));

            // Act
            var result = await _controller.DeleteSearch(999, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteSearch_WhenNotAuthenticated_ReturnsUnauthorized()
        {
            // Arrange
            _profileMock.Setup(p => p.GetCurrentUserId()).Returns((Guid?)null);

            // Act
            var result = await _controller.DeleteSearch(1, CancellationToken.None);

            // Assert
            Assert.IsType<UnauthorizedResult>(result);
        }
    }
}
