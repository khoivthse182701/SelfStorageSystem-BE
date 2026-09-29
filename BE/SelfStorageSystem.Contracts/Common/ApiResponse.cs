namespace SelfStorageSystem.Contracts.Common;

public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success.")
    {
        return new ApiResponse<T>
        {
            Success = true,
            Message = message,
            Data = data
        };
    }

    public static ApiResponse<T> Fail(string message, List<string>? errors = null)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok(string message = "Success.")
    {
        return new ApiResponse
        {
            Success = true,
            Message = message,
            Data = null
        };
    }

    public static new ApiResponse Fail(string message, List<string>? errors = null)
    {
        return new ApiResponse
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }
}

/// <summary>
/// Structured error response emitted by <c>AppExceptionMiddleware</c> for all typed domain errors.
/// </summary>
/// <param name="ErrorCode">Machine-readable dot-notation error code (e.g. "Auth.InvalidCredentials").</param>
/// <param name="Message">Human-readable error description.</param>
public record ApiErrorResponse(string ErrorCode, string Message)
{
    public bool Success { get; } = false;
}
