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

        var url = $"{baseUrl}/api/auth/login?username={Uri.EscapeDataString(username)}&password={Uri.EscapeDataString(password)}";

        var resp = await _http.PostAsync(url, content: null);

        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync();
            throw new Exception($"Login failed: {(int)resp.StatusCode} {resp.ReasonPhrase} | Body: {body}");
        }

        var json = await resp.Content.ReadFromJsonAsync<LoginResponse>();
        return json?.Token;
    }

    private class LoginResponse
    {
        public string? Token { get; set; }
    }
}