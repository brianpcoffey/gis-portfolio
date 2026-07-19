using Microsoft.AspNetCore.Mvc;
using Moq;
using Portfolio.Common.Constants;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;
using Xunit;

namespace Portfolio.Tests.Controllers
{
    public class UserProfileControllerTests
    {
        private readonly Mock<IUserProfileService> _serviceMock;
        private readonly UserProfileController _controller;
        private readonly Guid _testUserId = Guid.NewGuid();

        public UserProfileControllerTests()
        {
            _serviceMock = new Mock<IUserProfileService>();
            _controller  = new UserProfileController(_serviceMock.Object);
        }

        // ── GetCurrentProfile ───────────────────────────────────────────

        [Fact]
        public async Task GetCurrentProfile_WhenFound_ReturnsOkWithProfile()
        {
            // Arrange
            var dto = new ProfileDto { UserId = _testUserId };
            _serviceMock
                .Setup(s => s.GetCurrentProfileAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.GetCurrentProfile(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(dto, ok.Value);
        }

        [Fact]
        public async Task GetCurrentProfile_WhenProfileNotFound_ReturnsNotFound()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.GetCurrentProfileAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("Profile not found"));

            // Act
            var result = await _controller.GetCurrentProfile(CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundObjectResult>(result);
        }

        // ── UpdateCurrentProfile ────────────────────────────────────────

        [Fact]
        public async Task UpdateCurrentProfile_ReturnsOkWithUpdatedProfile()
        {
            // Arrange
            var dto     = new UpdateProfileDto { DisplayName = "Alice" };
            var updated = new ProfileDto { UserId = _testUserId };
            _serviceMock
                .Setup(s => s.UpdateCurrentProfileAsync(dto, It.IsAny<CancellationToken>()))
                .ReturnsAsync(updated);

            // Act
            var result = await _controller.UpdateCurrentProfile(dto, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(updated, ok.Value);
        }

        // ── GetById ─────────────────────────────────────────────────────

        [Fact]
        public async Task GetById_WhenSelf_ReturnsOk()
        {
            // Arrange — caller requests their own profile.
            _serviceMock.Setup(s => s.GetCurrentUserId()).Returns(_testUserId);
            var dto = new ProfileDto { UserId = _testUserId };
            _serviceMock
                .Setup(s => s.GetProfileByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.GetById(_testUserId, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(dto, ok.Value);
        }

        [Fact]
        public async Task GetById_WhenDifferentUser_ReturnsForbid()
        {
            // Arrange — caller requests someone else's profile (IDOR guard).
            _serviceMock.Setup(s => s.GetCurrentUserId()).Returns(Guid.NewGuid());

            // Act
            var result = await _controller.GetById(_testUserId, CancellationToken.None);

            // Assert — never reaches the profile lookup.
            Assert.IsType<ForbidResult>(result);
            _serviceMock.Verify(
                s => s.GetProfileByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task GetById_WhenSelfButNotFound_ReturnsNotFound()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetCurrentUserId()).Returns(_testUserId);
            _serviceMock
                .Setup(s => s.GetProfileByIdAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProfileDto?)null);

            // Act
            var result = await _controller.GetById(_testUserId, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // ── GetByGoogleId ───────────────────────────────────────────────

        [Fact]
        public async Task GetByGoogleId_WhenSelf_ReturnsOk()
        {
            // Arrange — the resolved profile belongs to the caller.
            _serviceMock.Setup(s => s.GetCurrentUserId()).Returns(_testUserId);
            var dto = new ProfileDto { UserId = _testUserId };
            _serviceMock
                .Setup(s => s.GetProfileByGoogleIdAsync("goog123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.GetByGoogleId("goog123", CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(dto, ok.Value);
        }

        [Fact]
        public async Task GetByGoogleId_WhenDifferentUser_ReturnsNotFound()
        {
            // Arrange — the resolved profile belongs to another user (IDOR guard).
            _serviceMock.Setup(s => s.GetCurrentUserId()).Returns(Guid.NewGuid());
            var dto = new ProfileDto { UserId = _testUserId };
            _serviceMock
                .Setup(s => s.GetProfileByGoogleIdAsync("goog123", It.IsAny<CancellationToken>()))
                .ReturnsAsync(dto);

            // Act
            var result = await _controller.GetByGoogleId("goog123", CancellationToken.None);

            // Assert — identical to the not-found case, so no account-existence oracle.
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task GetByGoogleId_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.GetProfileByGoogleIdAsync("unknown", It.IsAny<CancellationToken>()))
                .ReturnsAsync((ProfileDto?)null);

            // Act
            var result = await _controller.GetByGoogleId("unknown", CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // ── GetClaims ───────────────────────────────────────────────────

        [Fact]
        public async Task GetClaims_ReturnsOkWithClaimList()
        {
            // Arrange
            var claims = new List<ClaimDto>
            {
                new() { Type = "role",  Value = "admin" },
                new() { Type = "email", Value = "a@b.com" }
            };
            _serviceMock
                .Setup(s => s.GetClaimsAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(claims);

            // Act
            var result = await _controller.GetClaims(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var returned = Assert.IsAssignableFrom<List<ClaimDto>>(ok.Value);
            Assert.Equal(2, returned.Count);
        }

        // ── SetClaim ────────────────────────────────────────────────────

        [Fact]
        public async Task SetClaim_WithValidNonProtectedType_ReturnsNoContent()
        {
            // Arrange
            var dto = new ClaimDto { Type = "theme", Value = "dark" };
            _serviceMock
                .Setup(s => s.SetClaimAsync("theme", "dark", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.SetClaim(dto, CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Theory]
        [InlineData(null, "value")]
        [InlineData("", "value")]
        [InlineData("   ", "value")]
        public async Task SetClaim_WithMissingType_ReturnsBadRequest(string? type, string value)
        {
            // Arrange
            var dto = new ClaimDto { Type = type!, Value = value };

            // Act
            var result = await _controller.SetClaim(dto, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SetClaim_WithNullValue_ReturnsBadRequest()
        {
            // Arrange
            var dto = new ClaimDto { Type = "theme", Value = null! };

            // Act
            var result = await _controller.SetClaim(dto, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Theory]
        [InlineData(ProfileClaimTypes.GoogleId)]
        [InlineData(ProfileClaimTypes.Email)]
        [InlineData(ProfileClaimTypes.Name)]
        [InlineData(ProfileClaimTypes.Picture)]
        public async Task SetClaim_WithProtectedType_ReturnsBadRequest(string protectedType)
        {
            // Arrange
            var dto = new ClaimDto { Type = protectedType, Value = "somevalue" };

            // Act
            var result = await _controller.SetClaim(dto, CancellationToken.None);

            // Assert
            var bad = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(bad.Value);
        }

        // ── RemoveClaim ─────────────────────────────────────────────────

        [Fact]
        public async Task RemoveClaim_WhenRemoved_ReturnsNoContent()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.RemoveClaimAsync("theme", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.RemoveClaim("theme", CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task RemoveClaim_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            _serviceMock
                .Setup(s => s.RemoveClaimAsync("missing", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.RemoveClaim("missing", CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        // ── Delete ──────────────────────────────────────────────────────

        [Fact]
        public async Task Delete_WhenSelfDelete_ReturnsNoContent()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetCurrentUserId()).Returns(_testUserId);
            _serviceMock
                .Setup(s => s.DeleteProfileAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _controller.Delete(_testUserId, CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task Delete_WhenDifferentUser_ReturnsForbid()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetCurrentUserId()).Returns(Guid.NewGuid());

            // Act
            var result = await _controller.Delete(_testUserId, CancellationToken.None);

            // Assert
            Assert.IsType<ForbidResult>(result);
        }

        [Fact]
        public async Task Delete_WhenProfileNotFound_ReturnsNotFound()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetCurrentUserId()).Returns(_testUserId);
            _serviceMock
                .Setup(s => s.DeleteProfileAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _controller.Delete(_testUserId, CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}
