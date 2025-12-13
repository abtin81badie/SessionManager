using SessionManager.Application.Interfaces;

namespace SessionManager.Infrastructure.Persistence
{
    public class DatabaseSchemaInitializer : IDatabaseSchemaInitializer
    {
        private readonly PostgresDbContext _context;

        public DatabaseSchemaInitializer(PostgresDbContext context)
        {
            _context = context;
        }

        public async Task EnsureDatabaseCreatedAsync()
        {
            await _context.Database.EnsureCreatedAsync();
        }
    }
}