using System.Text.Json;
using NetworkObserver.Application.DTOs;
using NetworkObserver.Domain.Exceptions;

namespace NetworkObserver.Api.Middleware;

/// <summary>
/// Global exception handling middleware that intercepts all domain and unhandled exceptions,
/// formatting them into the authoritative Standardized Error Envelope defined in
/// api_contracts_specification.md Section 1.4.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, errorCode, message) = exception switch
        {
            UnauthenticatedException ex => (StatusCodes.Status401Unauthorized, ex.ErrorCode, ex.Message),
            InvalidCredentialsException ex => (StatusCodes.Status401Unauthorized, ex.ErrorCode, ex.Message),
            AccountDeactivatedException ex => (StatusCodes.Status401Unauthorized, ex.ErrorCode, ex.Message),
            UnauthorizedRoleException ex => (StatusCodes.Status403Forbidden, ex.ErrorCode, ex.Message),
            UserNotFoundException ex => (StatusCodes.Status404NotFound, ex.ErrorCode, ex.Message),
            DeviceNotFoundException ex => (StatusCodes.Status404NotFound, ex.ErrorCode, ex.Message),
            PasswordComplexityException ex => (StatusCodes.Status400BadRequest, ex.ErrorCode, ex.Message),
            PasswordReuseException ex => (StatusCodes.Status422UnprocessableEntity, ex.ErrorCode, ex.Message),
            MacAddressImmutableException ex => (StatusCodes.Status422UnprocessableEntity, ex.ErrorCode, ex.Message),
            DomainException ex => (StatusCodes.Status400BadRequest, ex.ErrorCode, ex.Message),
            _ => (StatusCodes.Status500InternalServerError, "INTERNAL_SERVER_ERROR", "An internal error occurred.")
        };

        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Unhandled system exception: {Message}", exception.Message);
        }
        else
        {
            _logger.LogWarning("Handled domain exception [{Code}]: {Message}", errorCode, message);
        }

        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.StatusCode = statusCode;

        var errorResponse = new ErrorResponse(
            new ErrorDetail(
                Code: errorCode,
                Message: message,
                Details: null,
                Timestamp: DateTimeOffset.UtcNow
            )
        );

        var json = JsonSerializer.Serialize(errorResponse, JsonOptions);
        await context.Response.WriteAsync(json);
    }
}
