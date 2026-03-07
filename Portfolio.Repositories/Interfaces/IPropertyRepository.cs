using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces
{
    public interface IPropertyRepository
    {
        Task<List<Property>> GetAllAsync(CancellationToken cancellationToken = default);
        Task<Property?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<List<Property>> GetFilteredAsync(
            decimal maxPrice,
            int minBedrooms,
            int minBathrooms,
            int minSqft,
            int maxCommuteMin,
            CancellationToken cancellationToken = default);
        Task<Property> AddAsync(Property property, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
        Task<int> CountAsync(CancellationToken cancellationToken = default);
    }
}