using SelfStorageSystem.Contracts.Common;
using SelfStorageSystem.Domain.Common;
using SelfStorageSystem.Domain.Exceptions;

namespace SelfStorageSystem.Middlewares;

/// <summary>
/// Global middleware that intercepts <see cref="IHasAppError"/> exceptions and maps them to
/// structured JSON responses instead of propagating raw 500s.
/// </summary>
public class AppExceptionMiddleware
{
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

            var statusCode = error.Type switch
            {
                ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
                ErrorType.Forbidden    => StatusCodes.Status403Forbidden,
                ErrorType.NotFound     => StatusCodes.Status404NotFound,
                ErrorType.Validation   => StatusCodes.Status400BadRequest,
                ErrorType.Conflict     => StatusCodes.Status409Conflict,
                _                     => StatusCodes.Status500InternalServerError
            };

            _logger.LogWarning(ex, "Handled AppException [{Code}] — {Description}", error.Code, error.Description);

            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/json";

            var response = new ApiErrorResponse(error.Code, error.Description);
            await context.Response.WriteAsJsonAsync(response);
        }
    }
}
