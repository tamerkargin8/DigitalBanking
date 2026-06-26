namespace DigitalBanking.Application.DTOs.Common;

/// <summary>
/// Standardized API response wrapper for all endpoints.
/// </summary>
public class ApiResponse<T>
{
    /// <summary>
    /// The response data payload.
    /// </summary>
    public T? Data { get; set; }

    /// <summary>
    /// Success status of the operation.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if operation failed.
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// List of validation errors or detailed error information.
    /// </summary>
    public List<string>? Errors { get; set; }

    /// <summary>
    /// UTC timestamp when response was generated.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Creates a successful response with data.
    /// </summary>
    public static ApiResponse<T> SuccessWith(T data, string? message = null)
    {
        return new ApiResponse<T>
        {
            Success = true,
            Data = data,
            Message = message ?? "Operation completed successfully.",
            Errors = null
        };
    }

    /// <summary>
    /// Creates a failed response with error message.
    /// </summary>
    public static ApiResponse<T> Failure(string message)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default,
            Message = message,
            Errors = null
        };
    }

    /// <summary>
    /// Creates a failed response with multiple errors.
    /// </summary>
    public static ApiResponse<T> Failure(string message, List<string> errors)
    {
        return new ApiResponse<T>
        {
            Success = false,
            Data = default,
            Message = message,
            Errors = errors
        };
    }
}

/// <summary>
/// Non-generic version for operations without return data.
/// </summary>
public class ApiResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public List<string>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse SuccessResponse(string? message = null)
    {
        return new ApiResponse
        {
            Success = true,
            Message = message ?? "Operation completed successfully.",
            Errors = null
        };
    }

    public static ApiResponse Failure(string message)
    {
        return new ApiResponse
        {
            Success = false,
            Message = message,
            Errors = null
        };
    }

    public static ApiResponse Failure(string message, List<string> errors)
    {
        return new ApiResponse
        {
            Success = false,
            Message = message,
            Errors = errors
        };
    }
}
