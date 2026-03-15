using Microsoft.AspNetCore.Http;
using Moq;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    public class UserProfileServiceTests
    {
        private readonly Mock<IUserProfileRepository> _repoMock;
        private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock;
        private readonly FakeTimeProvider _timeProvider;
        private readonly UserProfileService _service;
        private readonly Guid _testUserId = Guid.NewGuid();
        private readonly Mock<Portfolio.Repositories.PortfolioDbContext> _dbContextMock;
        private readonly Mock<UserProfileSeedService> _seedServiceMock;

        public UserProfileServiceTests()
        {
            _repoMock = new Mock<IUserProfileRepository>();
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero));

            // Setup DbContext mock for UserProfileSeedService
            var dbOptions = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Portfolio.Repositories.PortfolioDbContext>().Options;
            _dbContextMock = new Mock<Portfolio.Repositories.PortfolioDbContext>(dbOptions);
            _seedServiceMock = new Mock<UserProfileSeedService>(_dbContextMock.Object);

            var httpContext = new DefaultHttpContext();
            httpContext.Items["AnonUserId"] = _testUserId;
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

            _service = new UserProfileService(_httpContextAccessorMock.Object, _repoMock.Object, _timeProvider, _seedServiceMock.Object);
        }

        [Fact]
        public void GetCurrentUserId_WithValidContext_ReturnsUserId()
        {
            // Act
            var result = _service.GetCurrentUserId();

            // Assert
            Assert.Equal(_testUserId, result);
        }

        [Fact]
        public void GetCurrentUserId_WithNoContext_ReturnsNull()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            var service = new UserProfileService(_httpContextAccessorMock.Object, _repoMock.Object, _timeProvider, _seedServiceMock.Object);

            // Act
            var result = service.GetCurrentUserId();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void GetCurrentUserId_WithMissingAnonUserId_ReturnsNull()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
            var service = new UserProfileService(_httpContextAccessorMock.Object, _repoMock.Object, _timeProvider, _seedServiceMock.Object);

            // Act
            var result = service.GetCurrentUserId();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetClaimsAsync_ReturnsClaimDtos()
        {
            // Arrange
            var claims = new List<UserClaim>
            {
                new() { UserId = _testUserId, ClaimType = "role", ClaimValue = "admin" },
                new() { UserId = _testUserId, ClaimType = "email", ClaimValue = "test@example.com" }
            };
            _repoMock.Setup(r => r.GetClaimsAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(claims);

            // Act
            var result = await _service.GetClaimsAsync();

            // Assert
            Assert.Equal(2, result.Count);
            Assert.Contains(result, c => c.Type == "role" && c.Value == "admin");
            Assert.Contains(result, c => c.Type == "email" && c.Value == "test@example.com");
        }

        [Fact]
        public async Task GetClaimsAsync_WhenNoUser_ThrowsInvalidOperationException()
        {
            // Arrange
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            var service = new UserProfileService(_httpContextAccessorMock.Object, _repoMock.Object, _timeProvider, _seedServiceMock.Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GetClaimsAsync());
        }

        [Fact]
        public async Task GetClaimAsync_WithExistingClaim_ReturnsValue()
        {
            // Arrange
            var claim = new UserClaim { UserId = _testUserId, ClaimType = "role", ClaimValue = "admin" };
            _repoMock.Setup(r => r.GetClaimAsync(_testUserId, "role", It.IsAny<CancellationToken>())).ReturnsAsync(claim);

            // Act
            var result = await _service.GetClaimAsync("role");

            // Assert
            Assert.Equal("admin", result);
        }

        [Fact]
        public async Task GetClaimAsync_WithNonExistingClaim_ReturnsNull()
        {
            // Arrange
            _repoMock.Setup(r => r.GetClaimAsync(_testUserId, "missing", It.IsAny<CancellationToken>())).ReturnsAsync((UserClaim?)null);

            // Act
            var result = await _service.GetClaimAsync("missing");

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task SetClaimAsync_WithValidData_CallsRepository()
        {
            // Arrange
            _repoMock.Setup(r => r.SetClaimAsync(_testUserId, "role", "user", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            await _service.SetClaimAsync("role", "user");

            // Assert
            _repoMock.Verify(r => r.SetClaimAsync(_testUserId, "role", "user", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("", "value")]
        [InlineData("   ", "value")]
        [InlineData(null, "value")]
        public async Task SetClaimAsync_WithEmptyType_ThrowsArgumentException(string? type, string value)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.SetClaimAsync(type!, value));
        }

        [Fact]
        public async Task SetClaimAsync_WithNullValue_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.SetClaimAsync("role", null!));
        }

        [Fact]
        public async Task RemoveClaimAsync_WithExistingClaim_ReturnsTrue()
        {
            // Arrange
            _repoMock.Setup(r => r.RemoveClaimAsync(_testUserId, "role", It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            var result = await _service.RemoveClaimAsync("role");

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task RemoveClaimAsync_WithNonExistingClaim_ReturnsFalse()
        {
            // Arrange
            _repoMock.Setup(r => r.RemoveClaimAsync(_testUserId, "missing", It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            var result = await _service.RemoveClaimAsync("missing");

            // Assert
            Assert.False(result);
        }
    }
}