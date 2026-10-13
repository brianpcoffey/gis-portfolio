using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories;
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
        private readonly UserProfileSeedService _seedService;

        public UserProfileServiceTests()
        {
            _repoMock = new Mock<IUserProfileRepository>();
            _httpContextAccessorMock = new Mock<IHttpContextAccessor>();
            _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero));

            // SeedForUserAsync is never invoked by these unit tests — all repo calls are mocked.
            var dbOptions = new DbContextOptionsBuilder<PortfolioDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            var db = new PortfolioDbContext(dbOptions);
            _seedService = new UserProfileSeedService(db, NullLogger<UserProfileSeedService>.Instance);

            var httpContext = new DefaultHttpContext();
            httpContext.Items["PortfolioIdentity"] = _testUserId;
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);

            _service = new UserProfileService(
                _httpContextAccessorMock.Object,
                _repoMock.Object,
                _timeProvider,
                _seedService,
                NullLogger<UserProfileService>.Instance);
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
            var service = new UserProfileService(
                _httpContextAccessorMock.Object,
                _repoMock.Object,
                _timeProvider,
                _seedService,
                NullLogger<UserProfileService>.Instance);

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
            var service = new UserProfileService(
                _httpContextAccessorMock.Object,
                _repoMock.Object,
                _timeProvider,
                _seedService,
                NullLogger<UserProfileService>.Instance);

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
        public async Task SetClaimAsync_AtTheLengthLimits_IsAccepted()
        {
            // Arrange
            // Flipped from the WP-15 characterisation that accepted 500/5000 characters.
            var type = new string('t', 200);
            var value = new string('v', 1000);
            _repoMock.Setup(r => r.SetClaimAsync(_testUserId, type, value, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            await _service.SetClaimAsync(type, value);

            // Assert
            _repoMock.Verify(r => r.SetClaimAsync(_testUserId, type, value, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData(201, 5)]
        [InlineData(5, 1001)]
        public async Task SetClaimAsync_OverTheLengthLimits_ThrowsArgumentExceptionWithoutWriting(int typeLength, int valueLength)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.SetClaimAsync(new string('t', typeLength), new string('v', valueLength)));
            _repoMock.Verify(r => r.SetClaimAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetClaimAsync_NewTypeWhenUserHolds50Claims_ThrowsArgumentException()
        {
            // Arrange
            _repoMock.Setup(r => r.HasClaimAsync(_testUserId, "extra", It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _repoMock.Setup(r => r.CountClaimsAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(50);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.SetClaimAsync("extra", "v"));
            _repoMock.Verify(r => r.SetClaimAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetClaimAsync_ExistingTypeWhenUserHolds50Claims_UpdatesIt()
        {
            // Arrange
            _repoMock.Setup(r => r.HasClaimAsync(_testUserId, "role", It.IsAny<CancellationToken>())).ReturnsAsync(true);
            _repoMock.Setup(r => r.CountClaimsAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(50);
            _repoMock.Setup(r => r.SetClaimAsync(_testUserId, "role", "v", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            await _service.SetClaimAsync("role", "v");

            // Assert
            _repoMock.Verify(r => r.SetClaimAsync(_testUserId, "role", "v", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetClaimAsync_NewTypeWhenUserHolds49Claims_IsAccepted()
        {
            // Arrange
            _repoMock.Setup(r => r.CountClaimsAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(49);
            _repoMock.Setup(r => r.SetClaimAsync(_testUserId, "extra", "v", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            await _service.SetClaimAsync("extra", "v");

            // Assert
            _repoMock.Verify(r => r.SetClaimAsync(_testUserId, "extra", "v", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetClaimAsync_LogsOnlyTheRedactedUserId()
        {
            // Arrange
            var logger = new CapturingLogger<UserProfileService>();
            var service = new UserProfileService(_httpContextAccessorMock.Object, _repoMock.Object, _timeProvider, _seedService, logger);
            _repoMock.Setup(r => r.SetClaimAsync(_testUserId, "role", "v", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            // Act
            await service.SetClaimAsync("role", "v");

            // Assert
            var line = Assert.Single(logger.Lines);
            Assert.DoesNotContain(_testUserId.ToString(), line, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(Portfolio.Repositories.Diagnostics.LogRedaction.UserId(_testUserId), line, StringComparison.Ordinal);
        }

        [Fact]
        public async Task RemoveClaimAsync_WhenMissing_LogsOnlyTheRedactedUserId()
        {
            // Arrange
            var logger = new CapturingLogger<UserProfileService>();
            var service = new UserProfileService(_httpContextAccessorMock.Object, _repoMock.Object, _timeProvider, _seedService, logger);
            _repoMock.Setup(r => r.RemoveClaimAsync(_testUserId, "role", It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            await service.RemoveClaimAsync("role");

            // Assert
            var line = Assert.Single(logger.Lines);
            Assert.DoesNotContain(_testUserId.ToString(), line, StringComparison.OrdinalIgnoreCase);
        }

        private sealed class CapturingLogger<T> : ILogger<T>
        {
            public List<string> Lines { get; } = [];
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
                Lines.Add(formatter(state, exception));
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

        [Fact]
        public async Task GetProfileByIdAsync_WhenFound_ReturnsDtoWithClaims()
        {
            // Arrange
            var profile = new UserProfile { UserId = _testUserId };
            var claims  = new List<UserClaim>
            {
                new() { UserId = _testUserId, ClaimType = "role", ClaimValue = "admin" }
            };
            _repoMock.Setup(r => r.GetProfileAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
            _repoMock.Setup(r => r.GetClaimsAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(claims);

            // Act
            var result = await _service.GetProfileByIdAsync(_testUserId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(_testUserId, result!.UserId);
            Assert.Single(result.Claims);
        }

        [Fact]
        public async Task GetProfileByIdAsync_WhenNotFound_ReturnsNull()
        {
            // Arrange
            _repoMock.Setup(r => r.GetProfileAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync((UserProfile?)null);

            // Act
            var result = await _service.GetProfileByIdAsync(_testUserId);

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public async Task GetProfileByGoogleIdAsync_WhenFound_ReturnsDto()
        {
            // Arrange
            var profile = new UserProfile { UserId = _testUserId };
            _repoMock.Setup(r => r.GetProfileByClaimAsync("google_id", "goog123", It.IsAny<CancellationToken>())).ReturnsAsync(profile);
            _repoMock.Setup(r => r.GetClaimsAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserClaim>());

            // Act
            var result = await _service.GetProfileByGoogleIdAsync("goog123");

            // Assert
            Assert.NotNull(result);
            Assert.Equal(_testUserId, result!.UserId);
        }

        [Fact]
        public async Task GetProfileByGoogleIdAsync_WhenNotFound_ReturnsNull()
        {
            // Arrange
            _repoMock.Setup(r => r.GetProfileByClaimAsync("google_id", "unknown", It.IsAny<CancellationToken>())).ReturnsAsync((UserProfile?)null);

            // Act
            var result = await _service.GetProfileByGoogleIdAsync("unknown");

            // Assert
            Assert.Null(result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public async Task GetProfileByGoogleIdAsync_WithBlankId_ThrowsArgumentException(string? googleId)
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.GetProfileByGoogleIdAsync(googleId!));
        }

        [Fact]
        public async Task GetCurrentProfileAsync_WhenFound_ReturnsDto()
        {
            // Arrange
            var profile = new UserProfile { UserId = _testUserId };
            _repoMock.Setup(r => r.GetProfileAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
            _repoMock.Setup(r => r.GetClaimsAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserClaim>());

            // Act
            var result = await _service.GetCurrentProfileAsync();

            // Assert
            Assert.NotNull(result);
            Assert.Equal(_testUserId, result.UserId);
        }

        [Fact]
        public async Task GetCurrentProfileAsync_WhenProfileRowMissing_ThrowsInvalidOperationException()
        {
            // Arrange — user identity exists but no DB row
            _repoMock.Setup(r => r.GetProfileAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync((UserProfile?)null);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.GetCurrentProfileAsync());
        }

        [Fact]
        public async Task UpdateCurrentProfileAsync_WithOverlongDisplayName_Throws_AndWritesNothing()
        {
            // Arrange
            var dto = new UpdateProfileDto { DisplayName = new string('x', 1001) };

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() => _service.UpdateCurrentProfileAsync(dto));

            // Assert
            Assert.Contains("at most 1000", ex.Message);
            _repoMock.Verify(r => r.SetClaimAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task UpdateCurrentProfileAsync_WithDisplayNameOfExactlyTheLimit_IsAccepted()
        {
            // Arrange
            var name = new string('x', 1000);
            _repoMock.Setup(r => r.GetProfileAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(new UserProfile { UserId = _testUserId });
            _repoMock.Setup(r => r.GetClaimsAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserClaim>());

            // Act
            await _service.UpdateCurrentProfileAsync(new UpdateProfileDto { DisplayName = $"  {name}  " });

            // Assert
            _repoMock.Verify(r => r.SetClaimAsync(_testUserId, "display_name", name, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateCurrentProfileAsync_WithDisplayName_SetsClaimAndReturnsDto()
        {
            // Arrange
            var dto     = new UpdateProfileDto { DisplayName = "Alice" };
            var profile = new UserProfile { UserId = _testUserId };
            _repoMock.Setup(r => r.SetClaimAsync(_testUserId, "display_name", "Alice", It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            _repoMock.Setup(r => r.GetProfileAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
            _repoMock.Setup(r => r.GetClaimsAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserClaim>());

            // Act
            var result = await _service.UpdateCurrentProfileAsync(dto);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(_testUserId, result.UserId);
            _repoMock.Verify(r => r.SetClaimAsync(_testUserId, "display_name", "Alice", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task UpdateCurrentProfileAsync_WithNullDisplayName_RemovesClaimAndReturnsDto()
        {
            // Arrange
            var dto     = new UpdateProfileDto { DisplayName = null };
            var profile = new UserProfile { UserId = _testUserId };
            _repoMock.Setup(r => r.RemoveClaimAsync(_testUserId, "display_name", It.IsAny<CancellationToken>())).ReturnsAsync(false);
            _repoMock.Setup(r => r.GetProfileAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(profile);
            _repoMock.Setup(r => r.GetClaimsAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(new List<UserClaim>());

            // Act
            var result = await _service.UpdateCurrentProfileAsync(dto);

            // Assert
            Assert.NotNull(result);
            _repoMock.Verify(r => r.RemoveClaimAsync(_testUserId, "display_name", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task DeleteProfileAsync_WhenExists_ReturnsTrue()
        {
            // Arrange
            _repoMock.Setup(r => r.DeleteProfileAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

            // Act
            var result = await _service.DeleteProfileAsync(_testUserId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task DeleteProfileAsync_WhenNotFound_ReturnsFalse()
        {
            // Arrange
            _repoMock.Setup(r => r.DeleteProfileAsync(_testUserId, It.IsAny<CancellationToken>())).ReturnsAsync(false);

            // Act
            var result = await _service.DeleteProfileAsync(_testUserId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsGoogleLinkedAsync_WhenGoogleClaimExists_ReturnsTrue()
        {
            // Arrange
            var claim = new UserClaim { ClaimType = "google_id", ClaimValue = "goog123" };
            _repoMock.Setup(r => r.GetClaimAsync(_testUserId, "google_id", It.IsAny<CancellationToken>())).ReturnsAsync(claim);

            // Act
            var result = await _service.IsGoogleLinkedAsync();

            // Assert
            Assert.True(result);
        }

        [Fact]
        public async Task IsGoogleLinkedAsync_WhenNoGoogleClaim_ReturnsFalse()
        {
            // Arrange
            _repoMock.Setup(r => r.GetClaimAsync(_testUserId, "google_id", It.IsAny<CancellationToken>())).ReturnsAsync((UserClaim?)null);

            // Act
            var result = await _service.IsGoogleLinkedAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task IsGoogleLinkedAsync_WhenNoUser_ReturnsFalse()
        {
            // Arrange — no HTTP context → GetCurrentUserId() returns null
            _httpContextAccessorMock.Setup(x => x.HttpContext).Returns((HttpContext?)null);
            var service = new UserProfileService(
                _httpContextAccessorMock.Object,
                _repoMock.Object,
                _timeProvider,
                _seedService,
                NullLogger<UserProfileService>.Instance);

            // Act
            var result = await service.IsGoogleLinkedAsync();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public async Task CreateOrUpdateFromGoogleAsync_WhenExistingGoogleProfile_UpdatesLastActiveDate()
        {
            // Arrange
            var google  = new GoogleProfileDto { GoogleId = "goog1", Email = "a@b.com", Name = "Alice" };
            var profile = new UserProfile { UserId = _testUserId, LastActiveDate = DateTime.UtcNow.AddDays(-1) };
            _repoMock.Setup(r => r.GetProfileByClaimAsync("google_id", "goog1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);
            _repoMock.Setup(r => r.AddOrUpdateProfileAsync(It.IsAny<UserProfile>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);
            _repoMock.Setup(r => r.SetClaimAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
            // The service now wraps its writes in a transaction; make the mock run the callback.
            _repoMock.Setup(r => r.RunInTransactionAsync(It.IsAny<Func<CancellationToken, Task>>(), It.IsAny<CancellationToken>()))
                .Returns((Func<CancellationToken, Task> action, CancellationToken ct) => action(ct));

            // Act
            var userId = await _service.CreateOrUpdateFromGoogleAsync(google);

            // Assert
            Assert.Equal(_testUserId, userId);
            _repoMock.Verify(r => r.AddOrUpdateProfileAsync(It.Is<UserProfile>(p => p.UserId == _testUserId), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateOrUpdateFromGoogleAsync_WithNullDto_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _service.CreateOrUpdateFromGoogleAsync(null!));
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        public async Task CreateOrUpdateFromGoogleAsync_WithBlankGoogleId_ThrowsArgumentException(string googleId)
        {
            // Arrange
            var google = new GoogleProfileDto { GoogleId = googleId, Email = "a@b.com", Name = "Alice" };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                () => _service.CreateOrUpdateFromGoogleAsync(google));
        }
    }
}