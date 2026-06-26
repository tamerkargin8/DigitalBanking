using DigitalBanking.Dashboard.Services;

var builder = WebApplication.CreateBuilder(args);

// Razor Pages
builder.Services.AddRazorPages();

// HttpClient + Services
builder.Services.AddHttpClient();
builder.Services.AddScoped<ApiAuthService>();
builder.Services.AddScoped<ApiClient>();

// Session (JWT token saklamak için)
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Hata sayfası / HSTS
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Session middleware
app.UseSession();

// Razor Pages routing
app.MapRazorPages();

app.Run();