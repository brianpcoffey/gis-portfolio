using Portfolio.Common.Models;
using Microsoft.EntityFrameworkCore;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories
{
    public class UserNoteRepository : IUserNoteRepository
    {
        private readonly PortfolioDbContext _context;

        public UserNoteRepository(PortfolioDbContext context) => _context = context;

        public async Task<List<UserNote>> GetByFeatureIdAsync(int savedFeatureId, Guid userId, CancellationToken cancellationToken = default)
        {
            return await _context.UserNotes
                .Where(n => n.SavedFeatureId == savedFeatureId && n.UserId == userId)
                .ToListAsync(cancellationToken);
        }

        public async Task<UserNote> AddAsync(UserNote note, CancellationToken cancellationToken = default)
        {
            _context.UserNotes.Add(note);
            await _context.SaveChangesAsync(cancellationToken);
            return note;
        }

        public async Task<bool> DeleteAsync(int id, Guid userId, CancellationToken cancellationToken = default)
        {
            var note = await _context.UserNotes.FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken);
            if (note == null) return false;
            _context.UserNotes.Remove(note);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}