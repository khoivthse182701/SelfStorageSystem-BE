using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Domain.Common;
using SelfStorageSystem.Domain.Exceptions;
using System.Diagnostics;

namespace SelfStorageSystem.Middlewares;

/// <summary>
/// Global middleware that intercepts <see cref="IHasAppError"/> and unhandled exceptions
/// and maps them to structured <see cref="ApiResponse"/> JSON responses without leaking internals.
/// </summary>
public class AppExceptionMiddleware
{
    private static readonly HashSet<string> NotFoundErrorCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Reservation.NoPublishedRate"
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<AppExceptionMiddleware> _logger;

    public AppExceptionMiddleware(RequestDelegate next, ILogger<AppExceptionMiddleware> logger)
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
        catch (Exception ex) when (ex is IHasAppError appEx)
        {
            var error = appEx.Error;
            var statusCode = NotFoundErrorCodes.Contains(error.Code)
                ? StatusCodes.Status404NotFound
                : error.Type switch
                {
                    ErrorType.Unauthorized    => StatusCodes.Status401Unauthorized,
                    ErrorType.Forbidden       => StatusCodes.Status403Forbidden,
                    ErrorType.NotFound        => StatusCodes.Status404NotFound,
                    ErrorType.Validation      => StatusCodes.Status422UnprocessableEntity,
                    ErrorType.Conflict        => StatusCodes.Status409Conflict,
                    ErrorType.TooManyRequests => StatusCodes.Status429TooManyRequests,
                    _                         => StatusCodes.Status500InternalServerError
                };

            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            _logger.LogWarning(ex, "Handled AppException [{Code}] (HTTP {StatusCode}) [TraceId: {TraceId}] — {Description}",
                error.Code, statusCode, traceId, error.Description);

            if (context.Response.HasStarted) return;

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var response = ApiResponse.Fail(error.Description, error.Code, null, traceId);
            await context.Response.WriteAsJsonAsync(response);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            if (!context.Response.HasStarted)
                context.Response.StatusCode = StatusCodes.Status499ClientClosedRequest;
        }
        catch (Exception ex)
        {
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;
            _logger.LogError(ex, "Unhandled exception in HTTP pipeline [TraceId: {TraceId}]", traceId);

            if (context.Response.HasStarted) return;

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";

            var response = ApiResponse.Fail(
                "An internal system error occurred. Please contact support or try again later.",
                "System.InternalError",
                null,
                traceId);
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
