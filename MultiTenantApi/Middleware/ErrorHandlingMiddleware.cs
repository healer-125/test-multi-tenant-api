using MultiTenantApi.Services;

namespace MultiTenantApi.Middleware;

/// <summary>
/// Converts domain exceptions to appropriate HTTP status codes and a consistent JSON error body.
/// This keeps error-handling logic out of controllers.
/// </summary>
public class ErrorHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ErrorHandlingMiddleware> _logger;

    public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger)
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
        catch (TenantNotFoundException ex)
        {
            _logger.LogWarning("Tenant not found: {TenantId}", ex.TenantId);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error   = "TenantNotFound",
                message = ex.Message
            });
        }
        catch (TableNotFoundException ex)
        {
            _logger.LogWarning("Table not found: {TableName}", ex.TableName);
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error   = "TableNotFound",
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
            context.Response.StatusCode  = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error   = "InternalServerError",
                message = "An unexpected error occurred. Please try again later."
            });
        }
    }
}
