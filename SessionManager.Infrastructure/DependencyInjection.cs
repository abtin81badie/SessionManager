using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SessionManager.Application.Interfaces;
using SessionManager.Infrastructure.Options; // <--- CRITICAL: Must match where you moved the classes
using SessionManager.Infrastructure.Persistence;
using SessionManager.Infrastructure.Services;
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
            // We bind the sections here so the rest of Infrastructure can use them.
            services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
            services.Configure<AesOptions>(configuration.GetSection(AesOptions.SectionName));
            services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));

            // ==========================================================
            // 2. REDIS SETUP
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
            // 3. POSTGRES & EF CORE SETUP
            // ==========================================================
            services.AddDbContext<PostgresDbContext>(options =>
                options.UseNpgsql(
                    configuration.GetConnectionString("Postgres"),
                    b => b.MigrationsAssembly(typeof(PostgresDbContext).Assembly.FullName)
                ));

            services.AddScoped<IUserRepository, PostgresUserRepository>();

            // ==========================================================
            // 4. SECURITY SERVICES SETUP
            // ==========================================================
            services.AddScoped<ICryptoService, AesCryptoService>();
            services.AddScoped<ITokenService, JwtService>();

            return services;
        }
    }
}