using DotNetEnv;
using SessionManager.Api.Extensions;
using SessionManager.Api.Mapper;
using SessionManager.Api.Middleware;
using SessionManager.Application.Interfaces;
using SessionManager.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// --- 1. CONFIGURATION ---
void LoadEnvFile()
{
    // Try to find .env in current folder, or go up parent directories
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current != null)
    {
        var envPath = Path.Combine(current.FullName, ".env");
        if (File.Exists(envPath))
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[Startup] Loaded .env file from: {envPath}");
            Console.ResetColor();
            Env.Load(envPath);
            return;
        }
        current = current.Parent;
    }

    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[Startup] WARNING: .env file not found! Searched up from: {Directory.GetCurrentDirectory()}");
    Console.ResetColor();
}

LoadEnvFile();
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

// REPLACE DbInitializer with the new Service-based approach
using (var scope = app.Services.CreateScope())
{
    // Get the service we just registered above
    var seeder = scope.ServiceProvider.GetRequiredService<ISystemSeedService>();

    // Run the seeding logic
    await seeder.SeedAsync();
}

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