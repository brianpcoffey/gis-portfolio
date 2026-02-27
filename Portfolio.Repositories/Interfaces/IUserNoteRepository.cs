using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces
{
    public interface IUserNoteRepository
    {
        Task<List<UserNote>> GetByFeatureIdAsync(int savedFeatureId, Guid userId, CancellationToken cancellationToken = default);
        Task<UserNote> AddAsync(UserNote note, CancellationToken cancellationToken = default);
        Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default);
    }
}