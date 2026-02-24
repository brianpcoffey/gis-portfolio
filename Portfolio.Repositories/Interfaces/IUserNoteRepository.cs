using Portfolio.Common.Models;

namespace Portfolio.Repositories.Interfaces
{
    public interface IUserNoteRepository
    {
        Task<List<UserNote>> GetByFeatureIdAsync(int savedFeatureId);
        Task<UserNote> AddAsync(UserNote note);
        Task<bool> DeleteAsync(int id);
    }
}