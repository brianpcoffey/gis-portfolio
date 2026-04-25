using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Services.Interfaces;
using Portfolio.Web.Controllers.Api;
using Xunit;

namespace Portfolio.Tests.Controllers
{
    public class ProfileControllerTests
    {
        private readonly Mock<IUserProfileService> _serviceMock;
        private readonly ProfileController _controller;
        private readonly Guid _testUserId = Guid.NewGuid();

        public ProfileControllerTests()
        {
            _serviceMock = new Mock<IUserProfileService>();
            _controller = new ProfileController(
                _serviceMock.Object,
                new Mock<ILogger<ProfileController>>().Object);
        }

        [Fact]
        public async Task Get_WhenIdentityEstablished_ReturnsOkWithProfile()
        {
            // Arrange
            var claims = new List<ClaimDto> { new() { Type = "key", Value = "val" } };
            _serviceMock.Setup(s => s.GetCurrentUserId()).Returns(_testUserId);
            _serviceMock.Setup(s => s.GetClaimsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(claims);

            // Act
            var result = await _controller.Get(CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            var profile = Assert.IsType<ProfileDto>(ok.Value);
            Assert.Equal(_testUserId, profile.UserId);
            Assert.Single(profile.Claims);
        }

        [Fact]
        public async Task Get_WhenNoIdentity_ReturnsBadRequest()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetCurrentUserId()).Returns((Guid?)null);

            // Act
            var result = await _controller.Get(CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task SetClaim_WithValidBody_ReturnsOk()
        {
            // Arrange
            var body = new ClaimCreateDto { Type = "theme", Value = "dark" };
            _serviceMock.Setup(s => s.SetClaimAsync("theme", "dark", It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            var result = await _controller.SetClaim(body, CancellationToken.None);

            // Assert
            Assert.IsType<OkObjectResult>(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task SetClaim_WithMissingType_ReturnsBadRequest(string? type)
        {
            // Arrange
            var body = new ClaimCreateDto { Type = type!, Value = "v" };

            // Act
            var result = await _controller.SetClaim(body, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }

        [Fact]
        public async Task RemoveClaim_WhenFound_ReturnsNoContent()
        {
            // Arrange
            _serviceMock.Setup(s => s.RemoveClaimAsync("theme", It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            var result = await _controller.RemoveClaim("theme", CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task RemoveClaim_WhenNotFound_ReturnsNotFound()
        {
            // Arrange
            _serviceMock.Setup(s => s.RemoveClaimAsync("missing", It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            var result = await _controller.RemoveClaim("missing", CancellationToken.None);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task RemoveClaim_WithBlankType_ReturnsBadRequest(string type)
        {
            // Act
            var result = await _controller.RemoveClaim(type, CancellationToken.None);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
        }
    }
}
