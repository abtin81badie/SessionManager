using SessionManager.Api.Middleware;
using SessionManager.Infrastructure;
using SessionManager.Infrastructure.Persistence;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Swagger Configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Session Manager API",
        Version = "v1",
        Description = "API for managing user sessions with Redis (Max 2 Devices rule)."
    });

    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

// IOC REGISTRATION
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// AUTO-MIGRATION & SEEDING LOGIC
await DbInitializer.InitializeAsync(app.Services);

// HTTP REQUEST PIPELINE

// 1. Global Exception Handler
app.UseMiddleware<ExceptionMiddleware>();

// 2. Swagger
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SessionManager API V1");
    c.RoutePrefix = string.Empty;
});

app.UseAuthorization();

app.MapControllers();

app.Run();