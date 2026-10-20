using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DigitalBanking.Dashboard.Services;

public sealed record SetupStatus(bool NeedsOwner, bool SetupEnabled);
public sealed record SimpleOutcome(bool Success, string? Message)
{
    public static SimpleOutcome Ok(string? message) => new(true, message);
    public static SimpleOutcome Fail(string message) => new(false, message);
}

public sealed record LoginOutcome(
    bool Success,
    string? Token,
    string? Username,
    string? Role,
    Guid UserId,
    Guid? CustomerId,
    string? DisplayName,
    string? Error)
{
    public static LoginOutcome Ok(string token, Guid userId, string? username, string? role, Guid? customerId, string? displayName) =>
        new(true, token, username, role, userId, customerId, displayName, null);

    public static LoginOutcome Fail(string error) => new(false, null, null, null, Guid.Empty, null, null, error);
}

/// <summary>
/// Performs the anonymous login call. Failure bodies are never shown; callers get
/// a short, safe Turkish message instead.
/// </summary>
public class ApiAuthService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public ApiAuthService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task<LoginOutcome> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var baseUrl = _config["Api:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return LoginOutcome.Fail("Yerel API adresi yapılandırılmamış.");

        try
        {
            using var response = await _http.PostAsJsonAsync(
                $"{baseUrl}/api/auth/login",
                new { username, password },
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                return LoginOutcome.Fail("Çok fazla giriş denemesi yapıldı. Lütfen kısa bir süre sonra tekrar deneyin.");

            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized)
                return LoginOutcome.Fail("Kullanıcı adı veya şifre hatalı.");

            if (!response.IsSuccessStatusCode)
                return LoginOutcome.Fail("Giriş servisi şu anda yanıt vermiyor. Lütfen tekrar deneyin.");

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

            if (!document.RootElement.TryGetProperty("data", out var data)
                || !data.TryGetProperty("token", out var tokenElement)
                || string.IsNullOrWhiteSpace(tokenElement.GetString()))
                return LoginOutcome.Fail("Giriş yanıtı beklenen biçimde değil.");

            var usernameValue = data.TryGetProperty("username", out var usernameElement)
                ? usernameElement.GetString()
                : null;
            var roleValue = data.TryGetProperty("role", out var roleElement)
                ? roleElement.GetString()
                : null;
            Guid? customerId = data.TryGetProperty("customerId", out var customerElement)
                && Guid.TryParse(customerElement.GetString(), out var parsedCustomerId)
                ? parsedCustomerId
                : null;
            var displayName = data.TryGetProperty("displayName", out var displayElement)
                ? displayElement.GetString()
                : null;

            var userIdValue = data.TryGetProperty("userId", out var userElement) && Guid.TryParse(userElement.GetString(), out var parsedUserId)
                ? parsedUserId
                : Guid.Empty;
            return LoginOutcome.Ok(tokenElement.GetString()!, userIdValue, usernameValue, roleValue, customerId, displayName);
        }
        catch (TaskCanceledException)
        {
            return LoginOutcome.Fail("Giriş isteği zaman aşımına uğradı.");
        }
        catch (HttpRequestException)
        {
            return LoginOutcome.Fail("Giriş servisine ulaşılamıyor. Yerel API'nin çalıştığını kontrol edin.");
        }
        catch (JsonException)
        {
            return LoginOutcome.Fail("Giriş yanıtı işlenemedi.");
        }
    }

    public async Task<SetupStatus?> GetSetupStatusAsync(CancellationToken cancellationToken = default)
    {
        var baseUrl = _config["Api:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl)) return null;
        try
        {
            using var response = await _http.GetAsync($"{baseUrl}/api/setup/status", cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return new SetupStatus(
                document.RootElement.TryGetProperty("needsOwner", out var needsOwner) && needsOwner.GetBoolean(),
                document.RootElement.TryGetProperty("setupEnabled", out var setupEnabled) && setupEnabled.GetBoolean());
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public Task<SimpleOutcome> SetupOwnerAsync(
        string username, string email, string firstName, string lastName,
        string password, string confirmPassword, CancellationToken cancellationToken = default) =>
        PostAnonymousAsync("/api/setup/owner", new
        {
            username, email, firstName, lastName, password, confirmPassword
        }, "Owner oluşturuldu. Şimdi giriş yapabilirsiniz.", cancellationToken);

    public Task<SimpleOutcome> RegisterAsync(
        string username, string email, string firstName, string lastName,
        string password, string confirmPassword, CancellationToken cancellationToken = default) =>
        PostAnonymousAsync("/api/auth/register", new
        {
            username, email, firstName, lastName, password, confirmPassword
        }, "Üyelik başvurunuz alındı. Owner aktivasyonundan sonra giriş yapabilirsiniz.", cancellationToken);

    private async Task<SimpleOutcome> PostAnonymousAsync(
        string path, object body, string successMessage, CancellationToken cancellationToken)
    {
        var baseUrl = _config["Api:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            return SimpleOutcome.Fail("Yerel API adresi yapılandırılmamış.");

        try
        {
            using var response = await _http.PostAsJsonAsync(baseUrl + path, body, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return SimpleOutcome.Fail(ReadMessage(content) ?? "İşlem tamamlanamadı.");

            return SimpleOutcome.Ok(ReadMessage(content) ?? successMessage);
        }
        catch (HttpRequestException)
        {
            return SimpleOutcome.Fail("API'ye ulaşılamıyor. Yerel servislerin çalıştığını kontrol edin.");
        }
        catch (TaskCanceledException)
        {
            return SimpleOutcome.Fail("İstek zaman aşımına uğradı.");
        }
    }

    private static string? ReadMessage(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.TryGetProperty("message", out var message)
                ? message.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
