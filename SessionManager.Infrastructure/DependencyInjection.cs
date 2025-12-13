using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SessionManager.Application.Interfaces;
using SessionManager.Application.Options;
using SessionManager.Infrastructure.Options;
using SessionManager.Infrastructure.Persistence;
using SessionManager.Infrastructure.Repositories;
using SessionManager.Infrastructure.Services;
using SessionManager.Infrastructure.Validation;
using StackExchange.Redis;

namespace SessionManager.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // ==========================================================
            // 1. CONFIGURATION BINDING (Options Pattern)
            // ==========================================================
            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
            services.Configure<AesOptions>(configuration.GetSection(AesOptions.SectionName));
            services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));
            services.Configure<SessionOptions>(configuration.GetSection(SessionOptions.SectionName));
            services.Configure<RefreshTokenOptions>(configuration.GetSection(RefreshTokenOptions.SectionName));

            // ==========================================================
            // 2. VALIDATION SETUP 
            // ==========================================================
            services.AddScoped<ISessionValidator, SessionValidator>();

            // ==========================================================
            // 3. REDIS SETUP
            // ==========================================================
            string redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";

            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var config = ConfigurationOptions.Parse(redisConnectionString, true);
                config.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(config);
            });

            services.AddScoped<ISessionRepository, RedisSessionRepository>();

            // ==========================================================
            // 4. POSTGRES & EF CORE SETUP
            // ==========================================================
            services.AddDbContext<PostgresDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("Postgres"),
                    b => b.MigrationsAssembly(typeof(PostgresDbContext).Assembly.FullName)
                ));

            // Register the Schema Initializer (Used by SystemSeedService)
            services.AddScoped<IDatabaseSchemaInitializer, DatabaseSchemaInitializer>();

            // Register the User Repository for DOMAIN usage
            services.AddScoped<IUserRepository, PostgresUserRepository>();

            // Register the User Repository for SEEDER usage (Same implementation, different interface)
            services.AddScoped<IUserProvisioningRepository, PostgresUserRepository>();

            // ==========================================================
            // 5. SECURITY SERVICES SETUP
            // ==========================================================
            services.AddScoped<ICryptoService, AesCryptoService>();
            services.AddScoped<ITokenService, JwtService>();

            return services;
        }
    }
}