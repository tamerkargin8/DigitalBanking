using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using DigitalBanking.API.Extensions;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Auth;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Security;
using DigitalBanking.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace DigitalBanking.Tests;

public class AuthAndClaimsTests
{
    private static AuthService CreateAuthService(
        DigitalBanking.Infrastructure.Persistence.BankDbContext context,
        ILoginAttemptLimiter? limiter = null) =>
        new(context,
            new PasswordHasher(),
            new JwtTokenService(TestSupport.CreateConfiguration()),
            new AuditService(context),
            limiter ?? new InMemoryLoginAttemptLimiter());

    [Fact]
    public async Task SuccessfulLoginIssuesGuidClaimsWithIssuerAndAudience()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var hasher = new PasswordHasher();
        var user = await TestSupport.AddUserAsync(context, "admin-test", hasher.Hash("ValidPass1"), UserRole.Owner);
        var service = CreateAuthService(context);

        var response = await service.LoginAsync(new LoginRequest { Username = "admin-test", Password = "ValidPass1" });

        Assert.Equal("admin-test", response.Username);
        Assert.Equal("Owner", response.Role);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(response.Token);
        Assert.Equal("digitalbanking-tests", token.Issuer);
        Assert.Contains("digitalbanking-tests-client", token.Audiences);
        Assert.Equal(user.Id.ToString(), token.Claims.Single(c => c.Type == ClaimTypes.NameIdentifier).Value);
        Assert.Equal("admin-test", token.Claims.Single(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal("Owner", token.Claims.Single(c => c.Type == ClaimTypes.Role).Value);
        Assert.True(token.ValidTo > DateTime.UtcNow);
    }

    [Fact]
    public async Task FailedAttemptsAreThrottledAndAuditedWithoutPassword()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var hasher = new PasswordHasher();
        await TestSupport.AddUserAsync(context, "admin-test", hasher.Hash("ValidPass1"), UserRole.Owner);
        var limiter = new InMemoryLoginAttemptLimiter(maxFailures: 3, window: TimeSpan.FromMinutes(15), lockout: TimeSpan.FromMinutes(5));
        var service = CreateAuthService(context, limiter);

        for (var i = 0; i < 3; i++)
        {
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.LoginAsync(new LoginRequest { Username = "admin-test", Password = "WrongPass1" }));
        }

        await Assert.ThrowsAsync<TooManyRequestsException>(() =>
            service.LoginAsync(new LoginRequest { Username = "admin-test", Password = "ValidPass1" }));

        var audits = await context.AuditLogs.AsNoTracking().ToListAsync();
        Assert.Contains(audits, a => a.Action == "LOGIN_FAILED");
        Assert.Contains(audits, a => a.Action == "LOGIN_BLOCKED");
        Assert.DoesNotContain(audits, a => (a.Details ?? string.Empty).Contains("WrongPass1", StringComparison.Ordinal));
        Assert.DoesNotContain(audits, a => (a.Details ?? string.Empty).Contains("ValidPass1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SuccessfulLoginResetsFailureCounter()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var hasher = new PasswordHasher();
        await TestSupport.AddUserAsync(context, "admin-test", hasher.Hash("ValidPass1"), UserRole.Owner);
        var limiter = new InMemoryLoginAttemptLimiter(maxFailures: 3);
        var service = CreateAuthService(context, limiter);

        for (var i = 0; i < 2; i++)
            await Assert.ThrowsAsync<BusinessRuleException>(() =>
                service.LoginAsync(new LoginRequest { Username = "admin-test", Password = "WrongPass1" }));

        await service.LoginAsync(new LoginRequest { Username = "admin-test", Password = "ValidPass1" });
        Assert.Equal(LoginAttemptDecision.Allowed, limiter.Check("admin-test"));
        await service.LoginAsync(new LoginRequest { Username = "admin-test", Password = "ValidPass1" });
    }

    [Fact]
    public async Task LegacySha256HashIsUpgradedAfterSuccessfulLogin()
    {
        await using var db = await TestDb.CreateAsync();
        await using var context = db.NewContext();
        var legacy = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("LegacyPass1")));
        await TestSupport.AddUserAsync(context, "legacy-user", legacy, UserRole.Customer);
        var service = CreateAuthService(context);

        await service.LoginAsync(new LoginRequest { Username = "legacy-user", Password = "LegacyPass1" });

        var stored = await context.Users.AsNoTracking().SingleAsync(x => x.Username == "legacy-user");
        Assert.StartsWith("PBKDF2-SHA256$", stored.PasswordHash);
        Assert.False(new PasswordHasher().NeedsRehash(stored.PasswordHash));
    }

    [Fact]
    public void ActorIsBuiltFromValidatedClaims()
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, "claim-user"),
            new Claim(ClaimTypes.Role, "Owner")
        }, "test"));

        var actor = principal.GetActor();
        Assert.Equal(userId, actor.UserId);
        Assert.Equal("claim-user", actor.Username);
        Assert.True(actor.IsAdmin);
    }

    [Fact]
    public void MissingOrInvalidClaimsFailClosed()
    {
        Assert.Throws<UnauthorizedAccessException>(() => new ClaimsPrincipal(new ClaimsIdentity()).GetActor());
        var invalid = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "not-a-guid"),
            new Claim(ClaimTypes.Name, "claim-user"),
            new Claim(ClaimTypes.Role, "Owner")
        }, "test"));
        Assert.Throws<UnauthorizedAccessException>(() => invalid.GetActor());
    }

    [Fact]
    public void JwtGenerationFailsWhenIssuerIsMissing()
    {
        var config = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:SecretKey"] = "synthetic-test-signing-key-that-is-long-enough-0123456789"
            })
            .Build();
        var service = new JwtTokenService(config);

        Assert.Throws<InvalidOperationException>(() =>
            service.GenerateToken(new DigitalBanking.Domain.Entities.User
            {
                Id = Guid.NewGuid(),
                Username = "synthetic",
                Role = UserRole.Owner
            }));
    }
}
