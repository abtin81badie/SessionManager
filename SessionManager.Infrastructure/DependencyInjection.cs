using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SessionManager.Application.Interfaces;
using SessionManager.Infrastructure.Options;
using SessionManager.Infrastructure.Persistence;
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

            // ==========================================================
            // 2. VALIDATION SETUP 
            // ==========================================================
            // We register the Validator so the Repository can use it.
            // Since the Repository is Scoped, the Validator should also be Scoped.
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

            services.AddScoped<IUserRepository, PostgresUserRepository>();

            // ==========================================================
            // 5. SECURITY SERVICES SETUP
            // ==========================================================
            services.AddScoped<ICryptoService, AesCryptoService>();
            services.AddScoped<ITokenService, JwtService>();

            return services;
        }
    }
}