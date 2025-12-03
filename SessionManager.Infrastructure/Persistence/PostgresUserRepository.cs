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
            return await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        }

        public async Task CreateUserAsync(User user)
        {
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
        }
    }
}