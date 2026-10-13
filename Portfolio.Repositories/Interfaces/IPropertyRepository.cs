using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces
{
    public interface IPropertyRepository
    {
        Task<Property?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
        Task<List<Property>> GetFilteredAsync(
            decimal maxPrice,
            int minBedrooms,
            int minBathrooms,
            int minSqft,
            int maxCommuteMin,
            CancellationToken cancellationToken = default);
    }
}
