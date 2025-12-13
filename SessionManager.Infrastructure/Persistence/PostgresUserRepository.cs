using Microsoft.EntityFrameworkCore;
using SessionManager.Application.DTOs;       // Needed for Seeding DTO
using SessionManager.Application.Interfaces; // Needed for Interfaces
using SessionManager.Domain.Entities;        // Infrastructure is allowed to know Domain
using SessionManager.Infrastructure.Persistence; // Needed for DbContext

namespace SessionManager.Infrastructure.Repositories // Note: Usually in 'Repositories' folder, but keeping your namespace
{
    public class PostgresUserRepository : IUserRepository, IUserProvisioningRepository
    {
        private readonly PostgresDbContext _context;

        public PostgresUserRepository(PostgresDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // IUserRepository Implementation (Standard Domain Logic)
        // =========================================================

        public async Task<User?> GetByUsernameAsync(string username)
        {
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

        public async Task<User?> GetUserByIdAsync(Guid userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        // =========================================================
        // IUserProvisioningRepository Implementation (Seeding Logic)
        // =========================================================

        public async Task<bool> ExistsByUsernameAsync(string username)
        {
            return await _context.Users.AnyAsync(u => u.Username == username);
        }

        public async Task CreateUserAsync(CreateAdminDto userDto)
        {
            // MAPPING: Map the DTO to the Domain Entity here in Infrastructure
            var userEntity = new User
            {
                Id = Guid.NewGuid(),
                Username = userDto.Username,
                PasswordCipherText = userDto.PasswordCipherText,
                PasswordIV = userDto.PasswordIV,
                Role = userDto.Role
            };

            _context.Users.Add(userEntity);
            await _context.SaveChangesAsync();
        }
    }
}