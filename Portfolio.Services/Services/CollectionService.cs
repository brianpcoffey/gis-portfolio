using Microsoft.AspNetCore.Http;
using Portfolio.Common.DTOs;
using Portfolio.Common.Models;
using Portfolio.Repositories.Interfaces;
using Portfolio.Services.Interfaces;
using System.Security.Claims;

namespace Portfolio.Services.Services
{
    public class CollectionService : ICollectionService
    {
        private readonly ICollectionRepository _repository;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CollectionService(
            ICollectionRepository repository,
            IHttpContextAccessor httpContextAccessor)
        {
            _repository = repository
                ?? throw new ArgumentNullException(nameof(repository));

            _httpContextAccessor = httpContextAccessor
                ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        private string CurrentUserId
        {
            get
            {
                var userId = _httpContextAccessor
                    .HttpContext?
                    .User?
                    .FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (string.IsNullOrEmpty(userId))
                    throw new UnauthorizedAccessException("User not authenticated.");

                return userId;
            }
        }

        public async Task<List<CollectionDto>> GetAllAsync(
            CancellationToken cancellationToken = default)
        {
            var ownerId = CurrentUserId;

            var items = await _repository.GetAllAsync(
                ownerId,
                cancellationToken);

            return items
                .Select(MapToDto)
                .ToList();
        }

        public async Task<CollectionDto?> GetByIdAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var ownerId = CurrentUserId;

            var entity = await _repository.GetByIdAsync(
                id,
                ownerId,
                cancellationToken);

            return entity == null
                ? null
                : MapToDto(entity);
        }

        public async Task<CollectionDto> CreateAsync(
            CollectionCreateDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            if (string.IsNullOrWhiteSpace(dto.Name))
                throw new ArgumentException(
                    "Collection name is required.",
                    nameof(dto.Name));

            var ownerId = CurrentUserId;

            var name = dto.Name.Trim();

            if (await _repository.ExistsAsync(
                    ownerId,
                    name,
                    cancellationToken))
            {
                throw new InvalidOperationException(
                    "A collection with this name already exists.");
            }

            var entity = new Collection
            {
                OwnerId = ownerId,
                Name = name,
                Color = string.IsNullOrWhiteSpace(dto.Color)
                    ? "#6c757d"
                    : dto.Color.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            var created = await _repository.AddAsync(
                entity,
                cancellationToken);

            return MapToDto(created);
        }

        public async Task<CollectionDto> UpdateAsync(
            int id,
            CollectionUpdateDto dto,
            CancellationToken cancellationToken = default)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var ownerId = CurrentUserId;

            var entity = await _repository.GetByIdAsync(
                id,
                ownerId,
                cancellationToken)
                ?? throw new KeyNotFoundException(
                    $"Collection {id} not found.");

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                var newName = dto.Name.Trim();

                if (!string.Equals(newName, entity.Name, StringComparison.OrdinalIgnoreCase))
                {
                    if (await _repository.ExistsAsync(
                            ownerId,
                            newName,
                            cancellationToken))
                    {
                        throw new InvalidOperationException(
                            "A collection with this name already exists.");
                    }
                }

                entity.Name = newName;
            }

            if (!string.IsNullOrWhiteSpace(dto.Color))
            {
                entity.Color = dto.Color.Trim();
            }

            entity.LastModified = DateTime.UtcNow;

            var updated = await _repository.UpdateAsync(
                entity,
                cancellationToken);

            return MapToDto(updated);
        }

        public async Task<bool> DeleteAsync(
            int id,
            CancellationToken cancellationToken = default)
        {
            var ownerId = CurrentUserId;

            return await _repository.DeleteAsync(
                id,
                ownerId,
                cancellationToken);
        }

        private static CollectionDto MapToDto(Collection c)
        {
            return new CollectionDto
            {
                Id = c.Id,
                Name = c.Name,
                Color = c.Color,
                CreatedAt = c.CreatedAt,
                LastModified = c.LastModified
            };
        }
    }
}