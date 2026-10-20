using System.Net;
using System.Text;
using DigitalBanking.Dashboard.Pages;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace DigitalBanking.Tests;

public class SetupPageTests
{
    [Fact]
    public async Task GetRedirectsToLoginWhenOwnerAlreadyExists()
    {
        var handler = new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.OK,
            "{\"needsOwner\":false,\"setupEnabled\":false}")));
        var model = CreateModel(handler);

        var result = await model.OnGetAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Login", redirect.PageName);
        Assert.True((bool)redirect.RouteValues!["setup"]!);
    }

    [Fact]
    public async Task StalePostDoesNotCallOwnerEndpointAfterSetupCompleted()
    {
        var calls = new List<(HttpMethod Method, string Path)>();
        var handler = new StubHandler(request =>
        {
            calls.Add((request.Method, request.RequestUri!.AbsolutePath));
            return Task.FromResult(Json(HttpStatusCode.OK,
                "{\"needsOwner\":false,\"setupEnabled\":false}"));
        });
        var model = CreateModel(handler);
        FillForm(model);

        var result = await model.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Login", redirect.PageName);
        Assert.True((bool)redirect.RouteValues!["setup"]!);
        Assert.DoesNotContain(calls, call => call.Method == HttpMethod.Post);
        Assert.Empty(model.Password);
        Assert.Empty(model.ConfirmPassword);
    }

    [Fact]
    public async Task ConcurrentSetupWinnerRedirectsLateFormToLogin()
    {
        var calls = new List<(HttpMethod Method, string Path)>();
        var statusReads = 0;
        var handler = new StubHandler(request =>
        {
            calls.Add((request.Method, request.RequestUri!.AbsolutePath));
            if (request.Method == HttpMethod.Post)
                return Task.FromResult(Json(HttpStatusCode.Conflict,
                    "{\"success\":false,\"message\":\"Owner zaten kurulmuş. Kurulum endpoint'i kapatıldı; lütfen giriş yapın.\"}"));
            statusReads++;
            return Task.FromResult(Json(HttpStatusCode.OK,
                statusReads == 1
                    ? "{\"needsOwner\":true,\"setupEnabled\":true}"
                    : "{\"needsOwner\":false,\"setupEnabled\":false}"));
        });
        var model = CreateModel(handler);
        FillForm(model);

        var result = await model.OnPostAsync(CancellationToken.None);

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Login", redirect.PageName);
        Assert.True((bool)redirect.RouteValues!["setup"]!);
        Assert.Null(model.Error);
        Assert.Single(calls, call => call.Method == HttpMethod.Post);
        Assert.Equal(2, calls.Count(call => call.Method == HttpMethod.Get));
    }

    [Fact]
    public async Task RateLimitedSetupShowsTurkishMessageAndClearsPassword()
    {
        var handler = new StubHandler(request => Task.FromResult(request.Method == HttpMethod.Post
            ? Json(HttpStatusCode.TooManyRequests,
                "{\"success\":false,\"message\":\"Çok fazla kurulum denemesi yapıldı. Lütfen bir süre sonra tekrar deneyin.\"}")
            : Json(HttpStatusCode.OK, "{\"needsOwner\":true,\"setupEnabled\":true}")));
        var model = CreateModel(handler);
        FillForm(model);

        var result = await model.OnPostAsync(CancellationToken.None);

        var page = Assert.IsType<PageResult>(result);
        Assert.NotNull(page);
        Assert.Contains("Çok fazla kurulum", model.Error);
        Assert.DoesNotContain("Too many", model.Error);
        Assert.Empty(model.Password);
        Assert.Empty(model.ConfirmPassword);
    }

    [Fact]
    public async Task DisabledSetupShowsLocalDevelopmentMessage()
    {
        var handler = new StubHandler(_ => Task.FromResult(Json(HttpStatusCode.OK,
            "{\"needsOwner\":true,\"setupEnabled\":false}")));
        var model = CreateModel(handler);

        var result = await model.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains("localhost/Development", model.Error);
    }

    private static SetupModel CreateModel(HttpMessageHandler handler)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Api:BaseUrl"] = "http://127.0.0.1:5080"
            })
            .Build();
        return new SetupModel(new ApiAuthService(new HttpClient(handler), config));
    }

    private static void FillForm(SetupModel model)
    {
        model.Username = "late-owner";
        model.Email = "late-owner@demo.invalid";
        model.FirstName = "Late";
        model.LastName = "Owner";
        model.Password = "SyntheticOwner1";
        model.ConfirmPassword = "SyntheticOwner1";
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private sealed class StubHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) => callback(request);
    }
}
