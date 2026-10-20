using System.Net;
using System.Text;
using System.Text.Json;
using DigitalBanking.Dashboard.Services;
using Microsoft.Extensions.Configuration;

namespace DigitalBanking.Tests;

public class DashboardLoginContractTests
{
    [Fact]
    public async Task LoginSendsJsonBodyAndReadsWrappedToken()
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
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"success\":true,\"data\":{\"token\":\"synthetic-token\"}}", Encoding.UTF8, "application/json")
            };
        });
        using var client = new HttpClient(handler);
        var service = new ApiAuthService(client, Configuration());
        Assert.Equal("synthetic-token", await service.LoginAsync("demo-user", "synthetic-password"));
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task InvalidCredentialsReturnNoToken(HttpStatusCode code)
    {
        using var client = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(code))));
        Assert.Null(await new ApiAuthService(client, Configuration()).LoginAsync("demo", "wrong"));
    }

    [Fact]
    public async Task UnavailableApiIsReportedWithoutLeakingResponseBody()
    {
        using var client = new HttpClient(new StubHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        { Content = new StringContent("internal-sensitive-detail") })));
        var exception = await Assert.ThrowsAsync<HttpRequestException>(() => new ApiAuthService(client, Configuration()).LoginAsync("demo", "test"));
        Assert.DoesNotContain("internal-sensitive-detail", exception.Message);
    }

    private static IConfiguration Configuration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Api:BaseUrl"] = "http://127.0.0.1:5080" }).Build();

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => callback(request);
    }
}
