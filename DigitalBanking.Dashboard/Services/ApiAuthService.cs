using System.Net.Http.Json;

namespace DigitalBanking.Dashboard.Services;

public class ApiAuthService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;

    public ApiAuthService(HttpClient http, IConfiguration config)
    {
        _http = http;
        _config = config;
    }

    public async Task<string?> LoginAsync(string username, string password)
    {
        var baseUrl = _config["Api:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new Exception("Api:BaseUrl missing in Dashboard appsettings.json");

        var url = $"{baseUrl}/api/auth/login";
        using var resp = await _http.PostAsJsonAsync(url, new { username, password });

        if (!resp.IsSuccessStatusCode)
        {
            if (resp.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.Unauthorized)
                return null;
            throw new HttpRequestException("Authentication service is unavailable.");
        }

        var json = await resp.Content.ReadFromJsonAsync<LoginEnvelope>();
        return json?.Data?.Token;
    }

    private class LoginEnvelope
    {
        public LoginResponse? Data { get; set; }
    }

    private class LoginResponse
    {
        public string? Token { get; set; }
    }
}
