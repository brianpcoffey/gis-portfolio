using Moq;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;
using Portfolio.Services.Services;

namespace Portfolio.Tests.Services
{
    public class CollectionServiceTests
    {
        private readonly Mock<ICollectionRepository> _repoMock;
        private readonly Mock<IUserProfileService> _userProfileServiceMock;
        private readonly CollectionService _service;
        private readonly Guid _testUserId = Guid.NewGuid();
        private readonly FakeTimeProvider _timeProvider;

        public CollectionServiceTests()
        {
            _repoMock = new Mock<ICollectionRepository>();
            _userProfileServiceMock = new Mock<IUserProfileService>();
            _userProfileServiceMock.Setup(x => x.GetCurrentUserId()).Returns(_testUserId);

            _timeProvider = new FakeTimeProvider(new DateTimeOffset(2026, 3, 6, 12, 0, 0, TimeSpan.Zero));

            _service = new CollectionService(
                _repoMock.Object,
                _userProfileServiceMock.Object,
                _timeProvider);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsMappedDtos()
        {
            var collections = new List<Collection>
            {
                new() { Id = 1, OwnerId = _testUserId, Name = "Favorites", Color = "#ff0000", CreatedAt = DateTime.UtcNow },
                new() { Id = 2, OwnerId = _testUserId, Name = "Research", Color = "#00ff00", CreatedAt = DateTime.UtcNow }
            };
            _repoMock
                .Setup(r => r.GetAllAsync(_testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(collections);

            var result = await _service.GetAllAsync();

            Assert.Equal(2, result.Count);
            Assert.Equal("Favorites", result[0].Name);
            Assert.Equal("Research", result[1].Name);
        }

        [Fact]
        public async Task GetByIdAsync_WhenExists_ReturnsDto()
        {
            var entity = new Collection { Id = 1, OwnerId = _testUserId, Name = "Favorites", Color = "#ff0000", CreatedAt = DateTime.UtcNow };
            _repoMock
                .Setup(r => r.GetByIdAsync(1, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(entity);

            var result = await _service.GetByIdAsync(1);

            Assert.NotNull(result);
            Assert.Equal("Favorites", result!.Name);
        }

        [Fact]
        public async Task GetByIdAsync_WhenNotExists_ReturnsNull()
        {
            _repoMock
                .Setup(r => r.GetByIdAsync(999, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Collection?)null);

            var result = await _service.GetByIdAsync(999);

            Assert.Null(result);
        }

        [Fact]
        public async Task CreateAsync_WithValidDto_ReturnsDto()
        {
            var dto = new CollectionCreateDto { Name = "New Collection", Color = "#123456" };
            _repoMock
                .Setup(r => r.ExistsAsync(_testUserId, "New Collection", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _repoMock
                .Setup(r => r.AddAsync(It.IsAny<Collection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Collection c, CancellationToken _) => { c.Id = 1; return c; });

            var result = await _service.CreateAsync(dto);

            Assert.Equal(1, result.Id);
            Assert.Equal("New Collection", result.Name);
            Assert.Equal("#123456", result.Color);
        }

        [Fact]
        public async Task CreateAsync_UsesTimeProvider()
        {
            var dto = new CollectionCreateDto { Name = "Timed" };
            _repoMock
                .Setup(r => r.ExistsAsync(_testUserId, "Timed", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _repoMock
                .Setup(r => r.AddAsync(It.IsAny<Collection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Collection c, CancellationToken _) => { c.Id = 1; return c; });

            var result = await _service.CreateAsync(dto);

            Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, result.CreatedAt);
        }

        [Fact]
        public async Task CreateAsync_WithDuplicateName_ThrowsInvalidOperationException()
        {
            var dto = new CollectionCreateDto { Name = "Existing" };
            _repoMock
                .Setup(r => r.ExistsAsync(_testUserId, "Existing", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateAsync(dto));
        }

        [Fact]
        public async Task CreateAsync_WithNullName_ThrowsArgumentException()
        {
            var dto = new CollectionCreateDto { Name = null! };

            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(dto));
        }

        [Fact]
        public async Task CreateAsync_WithNullDto_ThrowsArgumentNullException()
        {
            await Assert.ThrowsAsync<ArgumentNullException>(() => _service.CreateAsync(null!));
        }

        [Fact]
        public async Task CreateAsync_WithBlankColor_DefaultsToGray()
        {
            var dto = new CollectionCreateDto { Name = "NoColor" };
            _repoMock
                .Setup(r => r.ExistsAsync(_testUserId, "NoColor", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _repoMock
                .Setup(r => r.AddAsync(It.IsAny<Collection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Collection c, CancellationToken _) => { c.Id = 1; return c; });

            var result = await _service.CreateAsync(dto);

            Assert.Equal("#6c757d", result.Color);
        }

        [Fact]
        public async Task UpdateAsync_WithValidDto_ReturnsUpdatedDto()
        {
            var existing = new Collection { Id = 1, OwnerId = _testUserId, Name = "Old", Color = "#aaa", CreatedAt = DateTime.UtcNow };
            _repoMock
                .Setup(r => r.GetByIdAsync(1, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            _repoMock
                .Setup(r => r.ExistsAsync(_testUserId, "Renamed", It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);
            _repoMock
                .Setup(r => r.UpdateAsync(It.IsAny<Collection>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Collection c, CancellationToken _) => c);

            var dto = new CollectionUpdateDto { Name = "Renamed", Color = "#bbb" };
            var result = await _service.UpdateAsync(1, dto);

            Assert.Equal("Renamed", result.Name);
            Assert.Equal("#bbb", result.Color);
            Assert.Equal(_timeProvider.GetUtcNow().UtcDateTime, result.LastModified);
        }

        [Fact]
        public async Task UpdateAsync_WhenNotFound_ThrowsKeyNotFoundException()
        {
            _repoMock
                .Setup(r => r.GetByIdAsync(999, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync((Collection?)null);

            await Assert.ThrowsAsync<KeyNotFoundException>(
                () => _service.UpdateAsync(999, new CollectionUpdateDto { Name = "X" }));
        }

        [Fact]
        public async Task UpdateAsync_WithDuplicateName_ThrowsInvalidOperationException()
        {
            var existing = new Collection { Id = 1, OwnerId = _testUserId, Name = "Old", Color = "#aaa" };
            _repoMock
                .Setup(r => r.GetByIdAsync(1, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(existing);
            _repoMock
                .Setup(r => r.ExistsAsync(_testUserId, "Taken", It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => _service.UpdateAsync(1, new CollectionUpdateDto { Name = "Taken" }));
        }

        [Fact]
        public async Task DeleteAsync_WhenExists_ReturnsTrue()
        {
            _repoMock
                .Setup(r => r.DeleteAsync(1, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            Assert.True(await _service.DeleteAsync(1));
        }

        [Fact]
        public async Task DeleteAsync_WhenNotExists_ReturnsFalse()
        {
            _repoMock
                .Setup(r => r.DeleteAsync(999, _testUserId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            Assert.False(await _service.DeleteAsync(999));
        }

        [Fact]
        public async Task GetAllAsync_WhenNoUser_ThrowsUnauthorizedAccessException()
        {
            _userProfileServiceMock.Setup(x => x.GetCurrentUserId()).Returns((Guid?)null);
            var service = new CollectionService(_repoMock.Object, _userProfileServiceMock.Object, _timeProvider);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetAllAsync());
        }
    }
}