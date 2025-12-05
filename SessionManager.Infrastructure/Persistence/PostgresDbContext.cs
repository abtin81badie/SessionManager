using Microsoft.EntityFrameworkCore;
using SessionManager.Domain.Entities;

namespace SessionManager.Infrastructure.Persistence
{
    public class PostgresDbContext : DbContext
    {
        public PostgresDbContext(DbContextOptions<PostgresDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configuration for the User Table
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.Username)
                    .IsRequired()
                    .HasMaxLength(50); // Best practice: Limit string length

                // CRITICAL: specific index for fast lookups by username
                entity.HasIndex(e => e.Username)
                    .IsUnique();

                // FIXED: Map the AES fields instead of PasswordHash
                entity.Property(e => e.PasswordCipherText)
                    .IsRequired();

                entity.Property(e => e.PasswordIV)
                    .IsRequired();

                entity.Property(e => e.Role)
                    .IsRequired()
                    .HasDefaultValue("User");
            });
        }
    }
}