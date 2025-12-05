using DotNetEnv;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SessionManager.Api.Middleware;
using SessionManager.Infrastructure;
using SessionManager.Infrastructure.Persistence;
using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// =========================================================================
//  1. DYNAMIC CONFIGURATION (LOCAL vs SERVER)
// =========================================================================

// Load .env file (Only needed for Local Development)
// On the server, Docker injects these values automatically, but loading it here doesn't hurt.
var rootEnvPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
Env.Load(rootEnvPath);
Env.Load();

// CRITICAL CHECK: Are we running inside a Docker Container?
// The "DOTNET_RUNNING_IN_CONTAINER" variable is automatically set to "true" inside .NET Docker images.
var isRunningInContainer = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true";

// Read variables from the Environment (or .env file)
var dbPassword = Environment.GetEnvironmentVariable("DB_PASSWORD");
var jwtSecretEnv = Environment.GetEnvironmentVariable("JWT_SECRET");
var aesKeyEnv = Environment.GetEnvironmentVariable("AES_KEY");
var adminUserEnv = Environment.GetEnvironmentVariable("ADMIN_USERNAME");
var adminPassEnv = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

// --- LOGIC: DATABASE CONNECTION ---
// If we are NOT in a container (running on Windows), we must connect to "localhost".
// If we ARE in a container (running on Server), we trust Docker Compose (which uses "Host=postgres").
if (!isRunningInContainer && !string.IsNullOrEmpty(dbPassword))
{
    // LOCAL MODE: Connect to localhost on Port 5433 (The port we mapped in docker-compose)
    var connString = $"Host=localhost;Port=5433;Database=SessionDb;Username=postgres;Password={dbPassword}";
    builder.Configuration["ConnectionStrings:Postgres"] = connString;
    Console.WriteLine($"[Config] Local Mode Detected: Connecting to Database at localhost:5433");
}
else
{
    // SERVER MODE: Do not overwrite ConnectionStrings. 
    // The app will use the value injected by Docker Compose (Host=postgres;Port=5432...)
    Console.WriteLine($"[Config] Container Mode Detected: Using Docker Configuration");
}

// --- LOGIC: SECURITY KEYS ---
// These are safe to inject in both modes
if (!string.IsNullOrEmpty(jwtSecretEnv)) builder.Configuration["JwtSettings:Secret"] = jwtSecretEnv;
if (!string.IsNullOrEmpty(aesKeyEnv)) builder.Configuration["AesSettings:Key"] = aesKeyEnv;

// --- LOGIC: ADMIN SETTINGS ---
if (!string.IsNullOrEmpty(adminUserEnv)) builder.Configuration["AdminSettings:Username"] = adminUserEnv;
if (!string.IsNullOrEmpty(adminPassEnv)) builder.Configuration["AdminSettings:Password"] = adminPassEnv;

// =========================================================================
//  2. SERVICE REGISTRATION
// =========================================================================

builder.Services.AddControllers();

// Add CORS (Crucial for Local React UI)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", b => b
        .AllowAnyOrigin()
        .AllowAnyMethod()
        .AllowAnyHeader());
});

// JWT Configuration
JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var jwtSecret = builder.Configuration["JwtSettings:Secret"];
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
        ValidateIssuer = true,
        ValidIssuer = "SessionManager",
        ValidateAudience = true,
        ValidAudience = "SessionManagerClient",
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };
});

// Swagger Configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Session Manager API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your valid token."
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });

    // XML Comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) c.IncludeXmlComments(xmlPath);
});

// Infrastructure
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// =========================================================================
//  3. PIPELINE
// =========================================================================

// Initialize Database (Seed Admin)
await DbInitializer.InitializeAsync(app.Services);

app.UseMiddleware<ExceptionMiddleware>();

// CORS must be before Auth
app.UseCors("AllowAll");

// Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SessionManager API V1");
    c.RoutePrefix = string.Empty; // Loads Swagger at Root URL
});

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();