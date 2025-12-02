using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SessionManager.Application.Interfaces;
using SessionManager.Infrastructure.Persistence;
using StackExchange.Redis;

namespace SessionManager.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            // 1. Redis Configuration
            string redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";

            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var config = ConfigurationOptions.Parse(redisConnectionString, true);
                config.AbortOnConnectFail = false;
                return ConnectionMultiplexer.Connect(config);
            });

            // 2. Register the Repository
            // This line caused the error before because ISessionRepository wasn't visible
            services.AddScoped<ISessionRepository, RedisSessionRepository>();

            return services;
        }
    }
}