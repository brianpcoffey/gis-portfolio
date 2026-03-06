using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;

namespace Portfolio.Services.Services
{
    public class CollectionService : ICollectionService
    {
        private readonly ICollectionRepository _repository;
        private readonly IUserProfileService _userProfileService;
        private readonly TimeProvider _timeProvider;

        public CollectionService(
            ICollectionRepository repository,
            IUserProfileService userProfileService,
            TimeProvider timeProvider)
        {
            _repository = repository
                ?? throw new ArgumentNullException(nameof(repository));
            _userProfileService = userProfileService
                ?? throw new ArgumentNullException(nameof(userProfileService));
            _timeProvider = timeProvider
                ?? throw new ArgumentNullException(nameof(timeProvider));
        }

        private Guid CurrentUserId =>
            _userProfileService.GetCurrentUserId()
                ?? throw new UnauthorizedAccessException("User not identified.");

        public async Task<List<CollectionDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            var items = await _repository.GetAllAsync(CurrentUserId, cancellationToken);
            return items.Select(MapToDto).ToList();
        }

        public async Task<CollectionDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var entity = await _repository.GetByIdAsync(id, CurrentUserId, cancellationToken);
            return entity == null ? null : MapToDto(entity);
        }

        public async Task<CollectionDto> CreateAsync(
            CollectionCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ArgumentException("Collection name is required.", nameof(dto));

            var ownerId = CurrentUserId;
            var name = dto.Name.Trim();

            if (await _repository.ExistsAsync(ownerId, name, cancellationToken))
                throw new InvalidOperationException("A collection with this name already exists.");

            var entity = new Collection
            {
                OwnerId = ownerId,
                Name = name,
                Color = string.IsNullOrWhiteSpace(dto.Color) ? "#6c757d" : dto.Color.Trim(),
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            };

            var created = await _repository.AddAsync(entity, cancellationToken);
            return MapToDto(created);
        }

        public async Task<CollectionDto> UpdateAsync(
            int id,
            CollectionUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(dto);

            var ownerId = CurrentUserId;
            var entity = await _repository.GetByIdAsync(id, ownerId, cancellationToken)
                ?? throw new KeyNotFoundException($"Collection {id} not found.");

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                var newName = dto.Name.Trim();
                if (!string.Equals(newName, entity.Name, StringComparison.OrdinalIgnoreCase)
                    && await _repository.ExistsAsync(ownerId, newName, cancellationToken))
                {
                    throw new InvalidOperationException("A collection with this name already exists.");
                }
                entity.Name = newName;
            }

            if (!string.IsNullOrWhiteSpace(dto.Color))
                entity.Color = dto.Color.Trim();

            entity.LastModified = _timeProvider.GetUtcNow().UtcDateTime;
            var updated = await _repository.UpdateAsync(entity, cancellationToken);
            return MapToDto(updated);
        }

        public async Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            return await _repository.DeleteAsync(id, CurrentUserId, cancellationToken);
        }

        private static CollectionDto MapToDto(Collection c) => new()
        {
            Id = c.Id,
            Name = c.Name,
            Color = c.Color,
            CreatedAt = c.CreatedAt,
            LastModified = c.LastModified
        };
    }
}