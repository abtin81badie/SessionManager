using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SessionManager.Application.Interfaces;
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
            // 1. REDIS SETUP
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
            // 2. POSTGRES & EF CORE SETUP
            // ==========================================================
            services.AddDbContext<PostgresDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("Postgres")));

            services.AddScoped<IUserRepository, PostgresUserRepository>();

            // ==========================================================
            // 3. SECURITY SERVICES SETUP
            // ==========================================================
            services.AddScoped<ICryptoService, AesCryptoService>();
            services.AddScoped<ITokenService, JwtService>();

            return services;
        }
    }
}