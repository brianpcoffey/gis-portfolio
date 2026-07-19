using Portfolio.Common.DTOs;

namespace Portfolio.Services.Interfaces
{
    /// <summary>
    /// Provides CRUD operations for feature collections.
    /// </summary>
    public interface ICollectionService
    {
        /// <summary>Returns all collections.</summary>
        Task<List<CollectionDto>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>Returns the collection with the given id, or <c>null</c> if not found.</summary>
        Task<CollectionDto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

        /// <summary>Creates a new collection and returns the persisted record.</summary>
        Task<CollectionDto> CreateAsync(CollectionCreateDto dto, CancellationToken cancellationToken = default);

        /// <summary>Updates the collection with the given id and returns the updated record.</summary>
        Task<CollectionDto> UpdateAsync(int id, CollectionUpdateDto dto, CancellationToken cancellationToken = default);

        /// <summary>Deletes the collection with the given id.</summary>
        /// <returns><c>true</c> if a row was deleted; otherwise <c>false</c>.</returns>
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    }
}