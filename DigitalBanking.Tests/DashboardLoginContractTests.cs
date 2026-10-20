using System.Net;
using System.Text;
using System.Text.Json;
using DigitalBanking.Dashboard.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace DigitalBanking.Tests;

public class DashboardLoginContractTests
{
    [Fact]
    public async Task LoginSendsJsonBodyAndReadsWrappedTokenAndRole()
    {
        var handler = new StubHandler(async request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/auth/login", request.RequestUri!.AbsolutePath);
            Assert.Empty(request.RequestUri.Query);
            Assert.Equal("application/json", request.Content!.Headers.ContentType!.MediaType);
            var body = JsonDocument.Parse(await request.Content.ReadAsStringAsync());
            Assert.Equal("demo-user", body.RootElement.GetProperty("username").GetString());
            Assert.Equal("synthetic-password", body.RootElement.GetProperty("password").GetString());
            return Json(HttpStatusCode.OK,
                "{\"success\":true,\"data\":{\"token\":\"synthetic-token\",\"username\":\"demo-user\",\"role\":\"Admin\"}}");
        });
        using var client = new HttpClient(handler);
        var service = new ApiAuthService(client, Configuration());
        var outcome = await service.LoginAsync("demo-user", "synthetic-password");

        Assert.True(outcome.Success);
        Assert.Equal("synthetic-token", outcome.Token);
        Assert.Equal("demo-user", outcome.Username);
        Assert.Equal("Admin", outcome.Role);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task InvalidCredentialsReturnSafeError(HttpStatusCode code)
    {
        using var client = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(code))));
        var outcome = await new ApiAuthService(client, Configuration()).LoginAsync("demo", "wrong");

        Assert.False(outcome.Success);
        Assert.Null(outcome.Token);
        Assert.Equal("Kullanıcı adı veya şifre hatalı.", outcome.Error);
    }

    [Fact]
    public async Task RateLimitedLoginReturnsFriendlyMessage()
    {
        using var client = new HttpClient(new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests))));
        var outcome = await new ApiAuthService(client, Configuration()).LoginAsync("demo", "test");

        Assert.False(outcome.Success);
        Assert.Contains("Çok fazla", outcome.Error);
    }

    [Fact]
    public async Task UnavailableApiIsReportedWithoutLeakingResponseBody()
    {
        using var client = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        { Content = new StringContent("internal-sensitive-detail") })));
        var outcome = await new ApiAuthService(client, Configuration()).LoginAsync("demo", "test");

        Assert.False(outcome.Success);
        Assert.DoesNotContain("internal-sensitive-detail", outcome.Error);
    }

    [Fact]
    public async Task ApiClientUnwrapsEnvelopeAndKeepsDecimalValues()
    {
        var handler = new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.OK,
            "{\"success\":true,\"data\":{\"items\":[{\"accountNumber\":\"ACC-1\",\"balance\":70.10,\"currency\":\"TRY\"}],\"totalCount\":1}}")));
        var api = CreateApiClient(handler);

        var result = await api.GetAsync<PagedAccounts>("token", "/api/accounts");

        Assert.True(result.Success);
        Assert.Equal(1, result.Data!.TotalCount);
        Assert.Equal(70.10m, result.Data.Items[0].Balance);
        Assert.Equal("TRY", result.Data.Items[0].Currency);
    }

    [Fact]
    public async Task ApiClientReadsRawLegacyArray()
    {
        var handler = new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.OK,
            "[{\"accountNumber\":\"ACC-2\",\"balance\":10.05,\"currency\":\"USD\"}]")));
        var api = CreateApiClient(handler);

        var result = await api.GetAsync<List<AccountRow>>("token", "/api/legacy");

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal(10.05m, result.Data![0].Balance);
    }

    [Fact]
    public async Task ApiClientMaps401ToSessionExpiryWithoutLeakingBody()
    {
        var handler = new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.Unauthorized,
            "{\"success\":false,\"message\":\"token expired: internal detail\"}")));
        var api = CreateApiClient(handler);

        var result = await api.GetAsync<List<AccountRow>>("stale-token", "/api/accounts");

        Assert.True(result.Unauthorized);
        Assert.Contains("Oturum", result.Error);
        Assert.DoesNotContain("internal detail", result.Error);
    }

    [Fact]
    public async Task ApiClientMapsForbiddenAndServerErrorsSafely()
    {
        var forbidden = CreateApiClient(new StubHandler(_ =>
            Task.FromResult(Json(HttpStatusCode.Forbidden, "{\"message\":\"raw detail\"}"))));
        var forbiddenResult = await forbidden.GetAsync<List<AccountRow>>("token", "/api/audit");
        Assert.True(forbiddenResult.Forbidden);
        Assert.Equal("Bu işlem için yetkiniz yok.", forbiddenResult.Error);

        var failure = CreateApiClient(new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            { Content = new StringContent("internal stack trace") })));
        var failureResult = await failure.GetAsync<List<AccountRow>>("token", "/api/accounts");
        Assert.False(failureResult.Success);
        Assert.DoesNotContain("internal stack trace", failureResult.Error);
    }

    [Fact]
    public async Task ApiClientSurfacesApiBusinessMessageButNotTransportDetails()
    {
        var handler = new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.BadRequest,
            "{\"success\":false,\"message\":\"Insufficient balance.\"}")));
        var api = CreateApiClient(handler);

        var result = await api.PostAsync<AccountRow>("token", "/api/transactions/withdraw",
            new { accountId = Guid.NewGuid(), amount = 10m }, idempotencyKey: "key-1");

        Assert.False(result.Success);
        Assert.Equal("Insufficient balance.", result.Error);
    }

    [Fact]
    public async Task ApiClientSendsIdempotencyKeyOnMoneyRequests()
    {
        string? key = null;
        var handler = new StubHandler(request =>
        {
            key = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
            return Task.FromResult(Json(HttpStatusCode.OK, "{\"success\":true,\"data\":{\"accountNumber\":\"ACC-3\"}}"));
        });
        var api = CreateApiClient(handler);

        var result = await api.PostAsync<AccountRow>("token", "/api/transactions/deposit",
            new { accountId = Guid.NewGuid(), amount = 1m }, idempotencyKey: "synthetic-key");

        Assert.True(result.Success);
        Assert.Equal("synthetic-key", key);
    }

    private static ApiClient CreateApiClient(HttpMessageHandler handler) =>
        new(new StubFactory(handler), Configuration(), NullLogger<ApiClient>.Instance);

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Api:BaseUrl"] = "http://127.0.0.1:5080" })
        .Build();

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            callback(request);
    }

    private sealed class StubFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class PagedAccounts
    {
        public List<AccountRow> Items { get; set; } = new();
        public int TotalCount { get; set; }
    }

    private sealed class AccountRow
    {
        public string AccountNumber { get; set; } = "";
        public decimal Balance { get; set; }
        public string Currency { get; set; } = "";
    }
}
