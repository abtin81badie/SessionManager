using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SessionManager.Api.Middleware;
using SessionManager.Infrastructure;
using SessionManager.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt; // Required for Claim Mapping fix
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// 1. JWT CONFIGURATION

// CRITICAL FIX: Stop ASP.NET from renaming 'sub' to 'http://schemas.xmlsoap.org/.../nameidentifier'
// Your JwtService uses standard names. This ensures Program.cs understands them.
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

// Get Secret from Config
var jwtSecret = builder.Configuration["JwtSettings:Secret"];
// Fallback if config is missing (matches your request)
if (string.IsNullOrEmpty(jwtSecret)) jwtSecret = "ThisIsASecretKeyForJwtTokenGenerationMustBeLongEnough";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        // 1. Validate the Issuer (Must match "SessionManager" in JwtService.cs)
        ValidateIssuer = true,
        ValidIssuer = "SessionManager",

        // 2. Validate the Audience (Must match "SessionManagerClient" in JwtService.cs)
        ValidateAudience = true,
        ValidAudience = "SessionManagerClient",

        // 3. Validate the Key
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),

        // 4. Validate Lifetime (handle clock differences)
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"[Auth Error]: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine($"[Auth Success]: Token validated for user.");
            return Task.CompletedTask;
        },
        OnMessageReceived = context =>
        {
            // Log the token receiving process
            var token = context.Request.Headers["Authorization"].FirstOrDefault();
            Console.WriteLine($"[Auth Info]: Authorization Header found: {token != null}");
            return Task.CompletedTask;
        }
    };
});

// 2. SWAGGER CONFIGURATION (Fixed for "Bearer " prefix)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Session Manager API",
        Version = "v1",
        Description = "API for managing user sessions."
    });

    // Define the Security Scheme
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http, // Changed from ApiKey to Http
        Scheme = "Bearer",              // This automatically adds "Bearer " prefix
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your valid token in the text input below.\r\n\r\nExample: \"eyJ...\""
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });

    // Optional: XML Comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// IOC REGISTRATION
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// AUTO-MIGRATION & SEEDING LOGIC
await DbInitializer.InitializeAsync(app.Services);

// HTTP REQUEST PIPELINE
// 1. Global Exception 
app.UseMiddleware<ExceptionMiddleware>();
// 2. Swagger

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SessionManager API V1");
    c.RoutePrefix = string.Empty;
});

// AUTH MIDDLEWARE (Order is strictly important)
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();