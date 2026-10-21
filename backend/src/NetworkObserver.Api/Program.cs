using System.Text.Json;
using Microsoft.Extensions.FileProviders;
using NetworkObserver.Api.Middleware;
using NetworkObserver.Application;
using NetworkObserver.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

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

// Locate compiled Vue 3 SPA frontend assets
var candidateDistPaths = new[]
{
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "..", "frontend", "dist")),
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", "..", "frontend", "dist")),
    Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "frontend", "dist")),
    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "wwwroot"))
};

var staticFilesPath = candidateDistPaths.FirstOrDefault(Directory.Exists);

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
