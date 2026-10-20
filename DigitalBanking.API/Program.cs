using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using DigitalBanking.API.Filters;
using DigitalBanking.API.Middleware;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Auth;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.Mappings;
using DigitalBanking.Application.Validators.Auth;
using DigitalBanking.Infrastructure;
using DigitalBanking.Infrastructure.Persistence;
using DigitalBanking.Infrastructure.Security;
using DigitalBanking.Infrastructure.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog();

    builder.Services
        .AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        });

    var jwtSettings = builder.Configuration.GetSection("Jwt");
    var secretKey = jwtSettings["SecretKey"];
    if (string.IsNullOrWhiteSpace(secretKey) || Encoding.UTF8.GetByteCount(secretKey) < 32)
        throw new InvalidOperationException("Configure Jwt:SecretKey with at least 32 bytes using environment variables or user secrets.");

    var issuer = jwtSettings["Issuer"];
    var audience = jwtSettings["Audience"];
    if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        throw new InvalidOperationException("Configure Jwt:Issuer and Jwt:Audience.");

    builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
        };
    });

    builder.Services.AddAuthorization();

    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy("auth", httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
            context.HttpContext.Response.ContentType = "application/json; charset=utf-8";
            await context.HttpContext.Response.WriteAsync(
                JsonSerializer.Serialize(
                    ApiResponse.Failure("Too many login requests. Please wait and try again."),
                    new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                cancellationToken);
        };
    });

    var useSqlite = string.Equals(builder.Configuration["Database:Provider"], "Sqlite", StringComparison.OrdinalIgnoreCase);
    if (useSqlite && !builder.Environment.IsDevelopment())
        throw new InvalidOperationException("SQLite is supported only for local Development.");

    builder.Services.AddDbContext<BankDbContext>(options =>
    {
        var connection = builder.Configuration.GetConnectionString("DefaultConnection");
        if (useSqlite) options.UseSqlite(connection);
        else options.UseSqlServer(connection);
    });

    builder.Services.AddInfrastructureServices();
    builder.Services.AddScoped<IdempotencyFilter>();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(options =>
    {
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "DigitalBanking API",
            Version = "v1",
            Description = "Enterprise digital banking backend API"
        });

        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description = "JWT token giriniz. Sadece token değerini yapıştırın; Bearer yazmanıza gerek yoktur."
        });

        options.AddSecurityRequirement(new OpenApiSecurityRequirement
        {
            {
                new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference
                    {
                        Type = ReferenceType.SecurityScheme,
                        Id = "Bearer"
                    }
                },
                Array.Empty<string>()
            }
        });
    });

    builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());
    builder.Services.AddValidatorsFromAssemblyContaining<LoginRequestValidator>();
    builder.Services.AddFluentValidationAutoValidation();

    builder.Services.AddScoped<JwtTokenService>();
    builder.Services.AddSingleton<PasswordHasher>();
    builder.Services.AddScoped<IPaymentApprovalService, PaymentApprovalService>();
    builder.Services.AddScoped<IAuthService, AuthService>();
    builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();

    var app = builder.Build();

    app.UseMiddleware<ExceptionMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    if (!useSqlite) app.UseHttpsRedirection();

    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    if (app.Environment.IsDevelopment())
        app.MapGet("/health", () => Results.Ok(new { status = "ok", database = useSqlite ? "Sqlite" : "SqlServer" }));

    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BankDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();

        var demoPassword = builder.Configuration["Development:DemoPassword"]
            ?? throw new InvalidOperationException("Configure Development:DemoPassword for local demo accounts.");
        await DatabaseSeeder.SeedAsync(db, hasher, demoPassword);
    }

    app.Run();
}
finally
{
    Log.CloseAndFlush();
}
