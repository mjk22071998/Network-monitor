using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using NetworkObserver.Api.Middleware;
using NetworkObserver.Application;
using NetworkObserver.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Ensure appsettings.json is resolved from either current directory or AppContext.BaseDirectory
builder.Configuration
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

if (Directory.GetCurrentDirectory() != AppContext.BaseDirectory)
{
    builder.Configuration
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);
}

builder.Configuration
    .AddEnvironmentVariables()
    .AddCommandLine(args);

// Configure controllers with snake_case JSON naming policy matching api_contracts_specification.md
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
        options.JsonSerializerOptions.DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower;
    });

builder.Services.AddOpenApi();

// Tier 2: Register Application Layer Services
builder.Services.AddApplication();

// Tier 4: Register Infrastructure Layer (PostgreSQL EF Core DbContext, Repositories, Security & Services)
builder.Services.AddInfrastructure(builder.Configuration);

// Background Workers (Continuous 5s/30s Telemetry & Host Health Polling Daemon)
builder.Services.AddHostedService<NetworkObserver.Api.HostedServices.TelemetryPollingBackgroundService>();

var app = builder.Build();

// Standardized Error Envelope Middleware (catches all exceptions and formats uniform JSON)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

// Locate compiled Vue 3 SPA frontend assets robustly
var staticFilesPath = FindStaticFilesPath();

if (staticFilesPath != null)
{
    var fileProvider = new PhysicalFileProvider(staticFilesPath);
    app.UseDefaultFiles(new DefaultFilesOptions
    {
        FileProvider = fileProvider
    });
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = fileProvider
    });
}

// Session Authentication Middleware (extracts session from observer_session cookie or Bearer header)
app.UseMiddleware<SessionAuthenticationMiddleware>();

app.UseAuthorization();

app.MapControllers();

// SPA Fallback for client-side routing
if (staticFilesPath != null)
{
    app.MapFallbackToFile("index.html", new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(staticFilesPath)
    });
}

app.Run();

static string? FindStaticFilesPath()
{
    // 1. Direct wwwroot alongside assembly (e.g. dist/server/wwwroot)
    var localWwwroot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
    if (Directory.Exists(localWwwroot)) return localWwwroot;

    // 2. Walk up directory tree from AppContext.BaseDirectory to find "frontend/dist"
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null)
    {
        var frontendDist = Path.Combine(dir.FullName, "frontend", "dist");
        if (Directory.Exists(frontendDist)) return frontendDist;
        dir = dir.Parent;
    }

    // 3. Fallback: Walk up directory tree from CurrentDirectory
    dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir != null)
    {
        var frontendDist = Path.Combine(dir.FullName, "frontend", "dist");
        if (Directory.Exists(frontendDist)) return frontendDist;
        dir = dir.Parent;
    }

    return null;
}
