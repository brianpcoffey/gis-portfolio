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
                _serviceMock.Object);
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

        [Fact]
        public async Task Get_WhenNoIdentity_PinsExactErrorBody()
        {
            // Arrange
            _serviceMock.Setup(s => s.GetCurrentUserId()).Returns((Guid?)null);

            // Act
            var result = await _controller.Get(CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, "Anonymous identity not established.");
        }

        [Fact]
        public async Task SetClaim_NullBody_PinsExactErrorBody()
        {
            // Act
            var result = await _controller.SetClaim(null!, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, "Type and value required.");
        }

        [Fact]
        public async Task SetClaim_ProtectedType_PinsExactErrorBody()
        {
            // Arrange
            var type = Portfolio.Common.Constants.ProfileClaimTypes.GoogleId;

            // Act
            var result = await _controller.SetClaim(new ClaimCreateDto { Type = type, Value = "v" }, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, $"Claim type '{type}' cannot be set anonymously.");
            _serviceMock.Verify(s => s.SetClaimAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task RemoveClaim_BlankType_PinsExactErrorBody()
        {
            // Act
            var result = await _controller.RemoveClaim(" ", CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, "Type required.");
        }

        [Fact]
        public async Task RemoveClaim_ProtectedType_PinsExactErrorBody()
        {
            // Arrange
            var type = Portfolio.Common.Constants.ProfileClaimTypes.Name;

            // Act
            var result = await _controller.RemoveClaim(type, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, $"Claim type '{type}' cannot be removed anonymously.");
        }

        [Fact]
        public async Task SetAuthenticatedClaim_ValidClaim_StoresClaimAndEchoesRequest()
        {
            // Arrange
            var claim = new ClaimCreateDto { Type = "theme", Value = "dark" };

            // Act
            var result = await _controller.SetAuthenticatedClaim(claim, CancellationToken.None);

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Same(claim, ok.Value);
            _serviceMock.Verify(s => s.SetClaimAsync("theme", "dark", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetAuthenticatedClaim_NullBody_PinsExactErrorBody()
        {
            // Act
            var result = await _controller.SetAuthenticatedClaim(null!, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, "Type and value required.");
        }

        [Fact]
        public async Task SetAuthenticatedClaim_BlankType_PinsExactErrorBody()
        {
            // Act
            var result = await _controller.SetAuthenticatedClaim(
                new ClaimCreateDto { Type = "  ", Value = "v" }, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400, "Type and value required.");
        }

        [Theory]
        [InlineData("google_id")]
        [InlineData("EMAIL")]
        [InlineData("name")]
        [InlineData("picture")]
        public async Task SetAuthenticatedClaim_ProtectedType_PinsExactErrorBody(string type)
        {
            // Act
            // The protected-type set is case-insensitive and the message echoes the caller's casing.
            var result = await _controller.SetAuthenticatedClaim(
                new ClaimCreateDto { Type = type, Value = "v" }, CancellationToken.None);

            // Assert
            ControllerBodyAssert.ErrorBody(result, 400,
                $"Claim type '{type}' is managed by Google OAuth and cannot be set manually.");
            _serviceMock.Verify(s => s.SetClaimAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
