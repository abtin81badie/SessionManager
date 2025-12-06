using SessionManager.Infrastructure.Options;
namespace SessionManager.Api.Mapper;

public static class OptionMapper
{
    public static void ConfigureAll(WebApplicationBuilder builder)
    {
        MapEnvironmentVariables(builder.Configuration);
        ConfigureDatabase(builder.Configuration);
    }

    private static void MapEnvironmentVariables(ConfigurationManager config)
    {
        void Map(string envVar, string configKey)
        {
            var value = Environment.GetEnvironmentVariable(envVar);
            if (!string.IsNullOrEmpty(value)) config[configKey] = value;
        }

        // --- Security Mappings ---
        Map("JWT_SECRET", $"{JwtOptions.SectionName}:Secret");
        Map("JWT_EXPIRY", $"{JwtOptions.SectionName}:ExpiryMinutes");
        Map("AES_KEY", $"{AesOptions.SectionName}:Key");

        // --- Admin Mappings ---
        Map("ADMIN_USERNAME", $"{AdminOptions.SectionName}:Username");
        Map("ADMIN_PASSWORD", $"{AdminOptions.SectionName}:Password");

        // --- Session Mappings (NEW) ---
        Map("SESSION_MAX_CONCURRENT", $"{SessionOptions.SectionName}:MaxConcurrentSessions");
        Map("SESSION_TIMEOUT_MINUTES", $"{SessionOptions.SectionName}:SessionTimeoutMinutes");
    }

    private static void ConfigureDatabase(ConfigurationManager config)
    {
        var isRunningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";
        var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");

        if (!isRunningInContainer && !string.IsNullOrEmpty(dbPassword))
        {
            var connString = $"Host=localhost;Port=5433;Database=SessionDb;Username=postgres;Password={dbPassword}";
            config["ConnectionStrings:Postgres"] = connString;
            Console.WriteLine($"[OptionMapper] Local Mode: Database -> localhost:5433");
        }
    }
}