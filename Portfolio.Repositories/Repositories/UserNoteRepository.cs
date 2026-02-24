using Portfolio.Common.Models;
using Microsoft.EntityFrameworkCore;
using Portfolio.Repositories.Interfaces;

namespace Portfolio.Repositories.Repositories
{
    public class UserNoteRepository : IUserNoteRepository
    {
        private readonly PortfolioDbContext _context;
        public UserNoteRepository(PortfolioDbContext context) => _context = context;

        public async Task<List<UserNote>> GetByFeatureIdAsync(int savedFeatureId) =>
            await _context.UserNotes.Where(n => n.SavedFeatureId == savedFeatureId).ToListAsync();

        public async Task<UserNote> AddAsync(UserNote note)
        {
            _context.UserNotes.Add(note);
            await _context.SaveChangesAsync();
            return note;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            var note = await _context.UserNotes.FindAsync(id);
            if (note == null) return false;
            _context.UserNotes.Remove(note);
            await _context.SaveChangesAsync();
            return true;
        }
    }
}