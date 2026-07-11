using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Auth;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Infrastructure.Persistence;
using DigitalBanking.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly BankDbContext _context;
    private readonly PasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IAuditService _auditService;

    public AuthService(
        BankDbContext context,
        PasswordHasher passwordHasher,
        JwtTokenService jwtTokenService,
        IAuditService auditService)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _auditService = auditService;
    }

    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Username == request.Username, cancellationToken);

        if (user is null)
        {
            await _auditService.LogAsync(
                action: "LOGIN_FAILED",
                entityName: "User",
                username: request.Username,
                details: "Login failed. User not found.");

            throw new BusinessRuleException("Invalid username or password.");
        }

        var isPasswordValid = _passwordHasher.Verify(
            request.Password,
            user.PasswordHash);

        if (!isPasswordValid)
        {
            await _auditService.LogAsync(
                action: "LOGIN_FAILED",
                entityName: "User",
                entityId: user.Id.ToString(),
                username: user.Username,
                details: "Login failed. Invalid password.");

            throw new BusinessRuleException("Invalid username or password.");
        }

        var token = _jwtTokenService.GenerateToken(user);

        await _auditService.LogAsync(
            action: "LOGIN_SUCCESS",
            entityName: "User",
            entityId: user.Id.ToString(),
            username: user.Username,
            details: "User logged in successfully.");

        return new LoginResponse
        {
            Token = token,
            Username = user.Username,
            Role = user.Role.ToString()
        };
    }
}