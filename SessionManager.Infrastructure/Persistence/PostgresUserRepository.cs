using Microsoft.EntityFrameworkCore;
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;

namespace SessionManager.Infrastructure.Persistence
{
    public class PostgresUserRepository : IUserRepository
    {
        private readonly PostgresDbContext _context;

        public PostgresUserRepository(PostgresDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByUsernameAsync(string username)
        {
            // OPTIMIZATION: Use AsNoTracking() for read-only queries.
            return await _context.Users
               .AsNoTracking()
               .FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task CreateUserAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }

        public async Task<List<User>> GetUsersByIdsAsync(HashSet<Guid> userIds)
        {
            if (userIds == null || !userIds.Any())
                return new List<User>();

            return await _context.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToListAsync();
        }
    }
}