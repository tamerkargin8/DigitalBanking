using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DigitalBanking.Dashboard.Services;

/// <summary>
/// Small API client for Razor Pages. It understands both the ApiResponse envelope
/// and the raw JSON returned by a few legacy endpoints, and turns every transport
/// or HTTP failure into a safe Turkish message.
/// </summary>
public sealed class ApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;
    private readonly ILogger<ApiClient> _logger;

    public ApiClient(IHttpClientFactory factory, IConfiguration config, ILogger<ApiClient> logger)
    {
        _factory = factory;
        _config = config;
        _logger = logger;
    }

    public Task<ApiResult<T>> GetAsync<T>(string token, string path, CancellationToken cancellationToken = default) =>
        SendAsync<T>(token, HttpMethod.Get, path, null, null, cancellationToken);

    public Task<ApiResult<T>> PostAsync<T>(
        string token,
        string path,
        object? body = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<T>(token, HttpMethod.Post, path, body, idempotencyKey, cancellationToken);

    public async Task<ApiResult<string>> GetTextAsync(string token, string path, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
            return ApiResult<string>.Fail(401, "Oturum bulunamadı.");

        var baseUrl = _config["Api:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return ApiResult<string>.Fail(500, "Yerel API adresi yapılandırılmamış.");

        using var http = _factory.CreateClient();
        http.BaseAddress = new Uri(baseUrl);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.Timeout = TimeSpan.FromSeconds(30);
        try
        {
            using var response = await http.GetAsync(path, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (response.StatusCode is HttpStatusCode.Unauthorized)
                return ApiResult<string>.Fail(401, "Oturum süresi doldu.");
            if (!response.IsSuccessStatusCode)
                return ApiResult<string>.Fail((int)response.StatusCode, "Dosya indirilemedi.");
            return ApiResult<string>.Ok(content, (int)response.StatusCode);
        }
        catch (HttpRequestException)
        {
            return ApiResult<string>.Fail(503, "API'ye ulaşılamıyor.");
        }
        catch (TaskCanceledException)
        {
            return ApiResult<string>.Fail(504, "İstek zaman aşımına uğradı.");
        }
    }

    public Task<ApiResult<T>> PutAsync<T>(
        string token,
        string path,
        object? body = null,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default) =>
        SendAsync<T>(token, HttpMethod.Put, path, body, idempotencyKey, cancellationToken);

    public Task<ApiResult<T>> PostFileAsync<T>(
        string token,
        string path,
        byte[] content,
        string fileName,
        CancellationToken cancellationToken = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        var multipart = new MultipartFormDataContent();
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        multipart.Add(file, "file", fileName);
        request.Content = multipart;
        return SendRequestAsync<T>(token, request, path, cancellationToken);
    }

    private Task<ApiResult<T>> SendAsync<T>(
        string token,
        HttpMethod method,
        string path,
        object? body,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(method, path);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        return SendRequestAsync<T>(token, request, path, cancellationToken);
    }

    private async Task<ApiResult<T>> SendRequestAsync<T>(
        string token,
        HttpRequestMessage request,
        string path,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
            return ApiResult<T>.Fail(StatusCodes.Status401Unauthorized, "Oturum bulunamadı. Lütfen tekrar giriş yapın.");

        var baseUrl = _config["Api:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            _logger.LogError("Dashboard Api:BaseUrl is not configured.");
            return ApiResult<T>.Fail(StatusCodes.Status500InternalServerError, "Yerel API adresi yapılandırılmamış.");
        }

        using var http = _factory.CreateClient();
        http.BaseAddress = new Uri(baseUrl);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        http.Timeout = TimeSpan.FromSeconds(30);
        request.RequestUri = new Uri(baseUrl + path, UriKind.Absolute);

        try
        {
            using var response = await http.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.StatusCode is HttpStatusCode.Unauthorized)
                return ApiResult<T>.Fail(401, "Oturum süresi doldu. Lütfen tekrar giriş yapın.");

            if (response.StatusCode is HttpStatusCode.Forbidden)
                return ApiResult<T>.Fail(403, "Bu işlem için yetkiniz yok.");

            if (response.StatusCode is HttpStatusCode.NotFound)
                return ApiResult<T>.Fail(404, "Kayıt bulunamadı.");

            if (!response.IsSuccessStatusCode)
            {
                var message = TryReadFailureMessage(content)
                    ?? (response.StatusCode == HttpStatusCode.TooManyRequests
                        ? "Çok fazla istek gönderildi. Lütfen kısa bir süre sonra tekrar deneyin."
                        : "İşlem tamamlanamadı. Lütfen tekrar deneyin.");
                _logger.LogWarning("API request failed. Status: {Status}, Path: {Path}", (int)response.StatusCode, path);
                return ApiResult<T>.Fail((int)response.StatusCode, message);
            }

            if (string.IsNullOrWhiteSpace(content))
                return ApiResult<T>.Ok(default, (int)response.StatusCode);

            try
            {
                using var document = JsonDocument.Parse(content);
                var value = UnwrapData(document.RootElement);
                var data = value.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
                    ? default
                    : value.Deserialize<T>(SerializerOptions);
                return ApiResult<T>.Ok(data, (int)response.StatusCode);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "API response could not be parsed. Path: {Path}", path);
                return ApiResult<T>.Fail((int)response.StatusCode, "API yanıtı işlenemedi.");
            }
        }
        catch (TaskCanceledException)
        {
            return ApiResult<T>.Fail(504, "API isteği zaman aşımına uğradı.");
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "API is unavailable. Path: {Path}", path);
            return ApiResult<T>.Fail(503, "API'ye ulaşılamıyor. Yerel servislerin çalıştığını kontrol edin.");
        }
    }

    private static JsonElement UnwrapData(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("success", out _)
            && root.TryGetProperty("data", out var data))
            return data;

        return root;
    }

    private static string? TryReadFailureMessage(string content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        try
        {
            using var document = JsonDocument.Parse(content);
            if (document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("message", out var message)
                && message.ValueKind == JsonValueKind.String)
                return message.GetString();
        }
        catch (JsonException)
        {
            // Do not surface a non-JSON body; it may contain implementation details.
        }

        return null;
    }
}
