using System.Net.Http.Headers;

namespace DigitalBanking.Dashboard.Services;

public class ApiClient
{
    private readonly IHttpClientFactory _factory;
    private readonly IConfiguration _config;

    public ApiClient(IHttpClientFactory factory, IConfiguration config)
    {
        _factory = factory;
        _config = config;
    }

    public HttpClient Create(string token)
    {
        var baseUrl = _config["Api:BaseUrl"]?.TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new Exception("Api:BaseUrl missing in Dashboard appsettings.json");

        var http = _factory.CreateClient();
        http.BaseAddress = new Uri(baseUrl);
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return http;
    }
}