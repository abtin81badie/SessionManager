using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;

namespace SessionManager.Infrastructure.Persistence
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var services = scope.ServiceProvider;
                try
                {
                    // 1. Create Database if missing
                    var context = services.GetRequiredService<PostgresDbContext>();
                    await context.Database.EnsureCreatedAsync();

                    // 2. Seed Admin User
                    var userRepo = services.GetRequiredService<IUserRepository>();
                    var crypto = services.GetRequiredService<ICryptoService>();
                    // Uses PostgresDbContext for logging category since 'Program' is not accessible here
                    var logger = services.GetRequiredService<ILogger<PostgresDbContext>>();

                    var adminUser = await userRepo.GetByUsernameAsync("admin");
                    if (adminUser == null)
                    {
                        logger.LogInformation("No Admin found. Seeding default Admin user...");

                        var (cipherText, iv) = crypto.Encrypt("Admin123!");

                        var newAdmin = new User
                        {
                            Id = Guid.NewGuid(),
                            Username = "admin",
                            PasswordCipherText = cipherText,
                            PasswordIV = iv,
                            Role = "Admin"
                        };

                        await userRepo.CreateUserAsync(newAdmin);
                        logger.LogInformation("Admin seeded! Credentials: 'admin' / 'Admin123!'");
                    }
                }
                catch (Exception ex)
                {
                    // Generic logger fallback if specific one fails, or just re-use context logger
                    var logger = services.GetRequiredService<ILogger<PostgresDbContext>>();
                    logger.LogError(ex, "An error occurred during database migration or seeding.");
                }
            }
        }
    }
}