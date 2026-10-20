namespace DigitalBanking.Dashboard.Services;

/// <summary>
/// Result of a dashboard-to-API call. Technical exception details are logged by
/// <see cref="ApiClient"/> and never shown to the user.
/// </summary>
public sealed class ApiResult<T>
{
    public T? Data { get; init; }
    public string? Error { get; init; }
    public int StatusCode { get; init; }

    public bool Success => Error is null && StatusCode is >= 200 and < 300;
    public bool Unauthorized => StatusCode == 401;
    public bool Forbidden => StatusCode == 403;
    public bool NotFound => StatusCode == 404;

    public static ApiResult<T> Ok(T? data, int statusCode = 200) => new()
    {
        Data = data,
        StatusCode = statusCode
    };

    public static ApiResult<T> Fail(int statusCode, string error) => new()
    {
        StatusCode = statusCode,
        Error = error
    };
}
