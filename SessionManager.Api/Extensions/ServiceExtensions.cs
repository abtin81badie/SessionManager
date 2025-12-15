using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SessionManager.Application.Behaviors;
using SessionManager.Application.Common;
using SessionManager.Application.Interfaces;
using SessionManager.Application.Services;
using SessionManager.Infrastructure.Options;
using SessionManager.Infrastructure.Services;
using System.Reflection;
using System.Text;

namespace SessionManager.Api.Extensions
{
    public static class ServiceExtensions
    {
        public static IServiceCollection AddApiServices(this IServiceCollection services)
        {
            services.AddControllers();

            services.AddCors(options =>
            {
                options.AddPolicy("AllowAll", b => b.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());
            });

            // Context Accessors for CurrentUserService
            services.AddHttpContextAccessor();
            services.AddScoped<UserSessionContext>();

            return services;
        }

        public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
        {
            // Register FluentValidation
            services.AddValidatorsFromAssemblyContaining<LoginCommandValidator>();

            // Register MediatR & Pipelines
            services.AddMediatR(cfg =>
            {
                cfg.RegisterServicesFromAssemblyContaining<LoginCommandValidator>();
                cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
            });

            // Register System Seeder
            services.AddScoped<ISystemSeedService, SystemSeedService>();

            return services;
        }

        public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
        {
            var jwtOpts = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
            var jwtSecretKey = !string.IsNullOrEmpty(jwtOpts.Secret) ? jwtOpts.Secret : "DefaultSecretMustBeLongEnoughForSafety";

            services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOpts.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOpts.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };
            });

            return services;
        }

        public static IServiceCollection AddSwaggerDocumentation(this IServiceCollection services)
        {
            services.AddEndpointsApiExplorer();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "Session Manager API", Version = "v1" });

                // 1. Define Access Token (Bearer)
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "Bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Enter your expired Access Token here."
                });

                // 2. Define Refresh Token (Custom Header)
                c.AddSecurityDefinition("RefreshToken", new OpenApiSecurityScheme
                {
                    Name = "X-Refresh-Token", // <--- The name of the header we will read
                    Type = SecuritySchemeType.ApiKey, // ApiKey type allows custom headers
                    In = ParameterLocation.Header,
                    Description = "Enter your Refresh Token here."
                });

                // 3. Require Both in the UI
                c.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                        },
                        Array.Empty<string>()
                    },
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "RefreshToken" }
                        },
                        Array.Empty<string>()
                    }
                });

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
            });

            return services;
        }

        public static IApplicationBuilder UseSwaggerDocumentation(this IApplicationBuilder app)
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "SessionManager API V1");
                c.RoutePrefix = string.Empty;
            });

            return app;
        }
    }
}