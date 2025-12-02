using Microsoft.EntityFrameworkCore;
using SessionManager.Domain.Entities;

namespace SessionManager.Infrastructure.Persistence
{
    public class PostgresDbContext : DbContext
    {
        public PostgresDbContext(DbContextOptions<PostgresDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
    }
}