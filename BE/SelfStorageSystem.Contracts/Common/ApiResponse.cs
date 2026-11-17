namespace SelfStorageSystem.Contracts.Common;

public interface IApiResponse
{
    bool Success { get; }
    string? TraceId { get; set; }
}

public class ApiResponse<T> : IApiResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public T? Data { get; set; }
    public string? ErrorCode { get; set; }
    public object? Errors { get; set; }
    public string? TraceId { get; set; }

    public static ApiResponse<T> Ok(T data, string message = "Success.") =>
        new()
        {
            Success = true,
            Message = message,
            Data = data
        };

    public static ApiResponse<T> Fail(string message, List<string>? errors = null) =>
        new()
        {
            Success = false,
            Message = message,
            Errors = errors
        };

    public static ApiResponse<T> Fail(
        string message,
        string? errorCode,
        IDictionary<string, string[]>? errors = null,
        string? traceId = null) =>
        new()
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            Errors = errors,
            TraceId = traceId
        };
}

public class ApiResponse : ApiResponse<object>
{
    public static ApiResponse Ok(string message = "Success.") =>
        new()
        {
            Success = true,
            Message = message,
            Data = null
        };

    public static new ApiResponse Fail(string message, List<string>? errors = null) =>
        new()
        {
            Success = false,
            Message = message,
            Errors = errors
        };

    public static new ApiResponse Fail(
        string message,
        string? errorCode,
        IDictionary<string, string[]>? errors = null,
        string? traceId = null) =>
        new()
        {
            Success = false,
            Message = message,
            ErrorCode = errorCode,
            Errors = errors,
            TraceId = traceId
        };
}

/// <summary>
/// Legacy alias kept for Swagger ProducesResponseType metadata compatibility while matching the unified ApiResponse envelope.
/// </summary>
public record ApiErrorResponse(string ErrorCode, string Message, string? TraceId = null, IDictionary<string, string[]>? Errors = null)
{
    public bool Success { get; } = false;
    public object? Data { get; } = null;
}
