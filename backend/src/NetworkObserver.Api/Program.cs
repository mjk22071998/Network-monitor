using System.Text.Json;
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

app.UseHttpsRedirection();

// Session Authentication Middleware (extracts session from observer_session cookie or Bearer header)
app.UseMiddleware<SessionAuthenticationMiddleware>();

app.UseAuthorization();

app.MapControllers();

app.Run();
