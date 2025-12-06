using DotNetEnv;
using SessionManager.Api.Extensions;
using SessionManager.Api.Mapper;
using SessionManager.Api.Middleware;
using SessionManager.Infrastructure;
using SessionManager.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURATION ---
// Load .env files
var rootEnvPath = Path.Combine(Directory.GetCurrentDirectory(), "..", ".env");
Env.Load(rootEnvPath);
Env.Load();

// Map Environment variables to AppSettings
OptionMapper.ConfigureAll(builder);

// --- 2. SERVICE REGISTRATION ---
builder.Services
    .AddApiServices()
    .AddJwtAuthentication(builder.Configuration)
    .AddSwaggerDocumentation()
    .AddInfrastructure(builder.Configuration)
    .AddApplicationLayer();

var app = builder.Build();

// --- 3. HTTP PIPELINE ---
await DbInitializer.InitializeAsync(app.Services);

app.UseMiddleware<ExceptionMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwaggerDocumentation();
}

app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();