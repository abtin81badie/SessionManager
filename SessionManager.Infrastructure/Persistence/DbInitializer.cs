using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options; // Required for IOptions
using SessionManager.Application.Interfaces;
using SessionManager.Domain.Entities;
using SessionManager.Infrastructure.Options;

namespace SessionManager.Infrastructure.Persistence
{
    public static class DbInitializer
    {
        public static async Task InitializeAsync(IServiceProvider serviceProvider)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<PostgresDbContext>>();

                try
                {
                    // 1. Retrieve Strongly Typed Admin Options
                    var adminOptions = services.GetRequiredService<IOptions<AdminOptions>>().Value;

                    // 2. Determine Credentials (use Options, fall back to defaults if empty)
                    var adminUsername = !string.IsNullOrEmpty(adminOptions.Username)
                        ? adminOptions.Username
                        : "admin";

                    var adminPassword = !string.IsNullOrEmpty(adminOptions.Password)
                        ? adminOptions.Password
                        : "Admin123!";

                    // 3. Create Database if missing
                    var context = services.GetRequiredService<PostgresDbContext>();
                    await context.Database.EnsureCreatedAsync();

                    // 4. Seed Admin User
                    var userRepo = services.GetRequiredService<IUserRepository>();
                    var crypto = services.GetRequiredService<ICryptoService>();

                    // Check if admin exists
                    var adminUser = await userRepo.GetByUsernameAsync(adminUsername);

                    if (adminUser == null)
                    {
                        logger.LogInformation($"No Admin found. Seeding default Admin user '{adminUsername}'...");

                        // Encrypt the password
                        var (cipherText, iv) = crypto.Encrypt(adminPassword);

                        var newAdmin = new User
                        {
                            Id = Guid.NewGuid(),
                            Username = adminUsername,
                            PasswordCipherText = cipherText,
                            PasswordIV = iv,
                            Role = "Admin"
                        };

                        await userRepo.CreateUserAsync(newAdmin);
                        logger.LogInformation("Admin seeded successfully!");
                    }
                    else
                    {
                        logger.LogInformation("Admin user already exists. Skipping seed.");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "An error occurred during database migration or seeding.");
                }
            }
        }
    }
}