using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SessionManager.Application.DTOs;
using SessionManager.Application.Interfaces;
using SessionManager.Application.Options;

namespace SessionManager.Application.Services
{
   

    public class SystemSeedService : ISystemSeedService
    {
        private readonly IUserProvisioningRepository _userProvisioningRepository;
        private readonly ICryptoService _cryptoService;
        private readonly IDatabaseSchemaInitializer _dbSchemaInitializer;
        private readonly AdminOptions _adminOptions;
        private readonly ILogger<SystemSeedService> _logger;

        public SystemSeedService(
            IUserProvisioningRepository userProvisioningRepository,
            ICryptoService cryptoService,
            IDatabaseSchemaInitializer dbSchemaInitializer,
            IOptions<AdminOptions> adminOptions,
            ILogger<SystemSeedService> logger)
        {
            _userProvisioningRepository = userProvisioningRepository;
            _cryptoService = cryptoService;
            _dbSchemaInitializer = dbSchemaInitializer;
            _adminOptions = adminOptions.Value;
            _logger = logger;
        }

        public async Task SeedAsync()
        {
            try
            {
                // 1. Ensure Database Schema Exists
                // We use an abstraction here so the Application layer doesn't need to know about EF Core directly.
                await _dbSchemaInitializer.EnsureDatabaseCreatedAsync();

                // 2. Determine Credentials (use Options, fall back to defaults if empty)
                var adminUsername = !string.IsNullOrEmpty(_adminOptions.Username)
                    ? _adminOptions.Username
                    : "admin";

                var adminPassword = !string.IsNullOrEmpty(_adminOptions.Password)
                    ? _adminOptions.Password
                    : "Admin123!";

                // 3. Check if admin exists using the specialized Provisioning Repository
                if (await _userProvisioningRepository.ExistsByUsernameAsync(adminUsername))
                {
                    _logger.LogInformation("Admin user already exists. Skipping seed.");
                    return;
                }

                _logger.LogInformation($"No Admin found. Seeding default Admin user '{adminUsername}'...");

                // 4. Encrypt the password
                var (cipherText, iv) = _cryptoService.Encrypt(adminPassword);

                // 5. Create the DTO 
                // This object is safe for the Application layer (it is not a Domain Entity)
                var newAdminDto = new CreateAdminDto
                {
                    Username = adminUsername,
                    PasswordCipherText = cipherText,
                    PasswordIV = iv,
                    Role = "Admin"
                };

                // 6. Persist the user
                // The Repository implementation in Infrastructure handles mapping DTO -> Domain Entity
                await _userProvisioningRepository.CreateUserAsync(newAdminDto);

                _logger.LogInformation("Admin seeded successfully!");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during system initialization.");
                throw;
            }
        }
    }
}