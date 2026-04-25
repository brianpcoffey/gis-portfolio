using Portfolio.Common.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories
{
    public class UserNoteRepository : IUserNoteRepository
    {
        private readonly PortfolioDbContext _context;
        private readonly ILogger<UserNoteRepository> _logger;

        public UserNoteRepository(PortfolioDbContext context, ILogger<UserNoteRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

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
            if (note == null)
            {
                _logger.LogWarning("UserNote {NoteId} not found for user {UserId} during delete", id, userId);
                return false;
            }
            _context.UserNotes.Remove(note);
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}