using System.Globalization;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

builder.Services.AddScoped<ApiAuthService>();
builder.Services.AddScoped<ApiClient>();

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.Name = ".DigitalBanking.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});

var app = builder.Build();

// HTML number inputs submit invariant "25.50"; keep model binding invariant while
// the UI formats money explicitly with tr-TR in DashboardUi.
var invariant = CultureInfo.InvariantCulture;
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(invariant),
    SupportedCultures = new[] { invariant },
    SupportedUICultures = new[] { invariant }
});

// Always show the friendly error page: stack traces and implementation details
// must not reach the browser, even in the local Development demo.
app.UseExceptionHandler("/Error");
app.UseStatusCodePagesWithReExecute("/Error", "?code={0}");
if (!app.Environment.IsDevelopment())
    app.UseHsts();

if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.MapRazorPages();

app.Run();
