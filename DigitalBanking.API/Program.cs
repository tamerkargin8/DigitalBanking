using DigitalBanking.API.Middleware;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Auth;
using DigitalBanking.Application.Mappings;
using DigitalBanking.Application.Validators.Auth;
using DigitalBanking.Infrastructure;
using DigitalBanking.Infrastructure.Persistence;
using DigitalBanking.Infrastructure.Security;
using DigitalBanking.Infrastructure.Services;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Text.Json.Serialization;

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

            ValidIssuer = jwtSettings["Issuer"],
            ValidAudience = jwtSettings["Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey!))
        };
    });

    builder.Services.AddAuthorization();

    builder.Services.AddDbContext<BankDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddInfrastructureServices();

    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    builder.Services.AddAutoMapper(cfg =>
    {
        cfg.AddProfile<MappingProfile>();
    });

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

    app.UseHttpsRedirection();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<BankDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher>();

        await DatabaseSeeder.SeedAsync(db, hasher);
    }

    app.Run();
}
finally
{
    Log.CloseAndFlush();
}