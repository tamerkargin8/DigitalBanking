using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace DigitalBanking.Dashboard.Services;

public sealed record LoginOutcome(
    bool Success,
    string? Token,
    string? Username,
    string? Role,
    string? Error)
{
    public static LoginOutcome Ok(string token, string? username, string? role) =>
        new(true, token, username, role, null);

    public static LoginOutcome Fail(string error) => new(false, null, null, null, error);
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

            return LoginOutcome.Ok(tokenElement.GetString()!, usernameValue, roleValue);
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
}
