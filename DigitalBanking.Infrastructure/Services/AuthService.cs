using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Auth;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using DigitalBanking.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Infrastructure.Services;

public class AuthService : IAuthService
{
    private const string InvalidCredentialsMessage = "Invalid username or password.";

    private readonly BankDbContext _context;
    private readonly PasswordHasher _passwordHasher;
    private readonly JwtTokenService _jwtTokenService;
    private readonly IAuditService _auditService;
    private readonly ILoginAttemptLimiter _loginAttemptLimiter;

    public AuthService(
        BankDbContext context,
        PasswordHasher passwordHasher,
        JwtTokenService jwtTokenService,
        IAuditService auditService,
        ILoginAttemptLimiter loginAttemptLimiter)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _jwtTokenService = jwtTokenService;
        _auditService = auditService;
        _loginAttemptLimiter = loginAttemptLimiter;
    }

    public async Task<LoginResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var username = request.Username.Trim();

        if (_loginAttemptLimiter.Check(username) == LoginAttemptDecision.Blocked)
        {
            await _auditService.LogAsync(
                action: "LOGIN_BLOCKED",
                entityName: "User",
                username: username,
                isSuccess: false,
                details: "Login blocked by the failed-attempt throttle.");

            throw new TooManyRequestsException("Too many failed login attempts. Please wait and try again.");
        }

        var user = await _context.Users
            .FirstOrDefaultAsync(x => x.Username == username, cancellationToken);

        if (user is null)
        {
            _loginAttemptLimiter.RecordFailure(username);
            await _auditService.LogAsync(
                action: "LOGIN_FAILED",
                entityName: "User",
                username: username,
                isSuccess: false,
                details: "Login failed. User not found.");

            throw new BusinessRuleException(InvalidCredentialsMessage);
        }

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            _loginAttemptLimiter.RecordFailure(username);
            await _auditService.LogAsync(
                action: "LOGIN_FAILED",
                entityName: "User",
                entityId: user.Id.ToString(),
                username: user.Username,
                isSuccess: false,
                details: "Login failed. Invalid password.");

            throw new BusinessRuleException(InvalidCredentialsMessage);
        }

        if (user.Status != UserStatus.Active)
        {
            await _auditService.LogAsync(
                action: "LOGIN_BLOCKED_STATUS",
                entityName: "User",
                entityId: user.Id.ToString(),
                username: user.Username,
                isSuccess: false,
                details: $"Login blocked. Status: {user.Status}");

            throw new ForbiddenException("Hesabınız henüz etkin değil veya devre dışı. Owner aktivasyonu gerekir.");
        }

        if (user.Role is not (UserRole.Owner or UserRole.DelegatedApprover or UserRole.Customer))
        {
            await _auditService.LogAsync(
                action: "LOGIN_BLOCKED_ROLE",
                entityName: "User",
                entityId: user.Id.ToString(),
                username: user.Username,
                isSuccess: false,
                details: $"Login blocked. Legacy/invalid role: {user.Role}");

            throw new ForbiddenException("Bu hesabın rolü geçerli değil. Owner ile iletişime geçin.");
        }

        _loginAttemptLimiter.RecordSuccess(username);

        if (_passwordHasher.NeedsRehash(user.PasswordHash))
        {
            user.PasswordHash = _passwordHasher.Hash(request.Password);
            await _context.SaveChangesAsync(cancellationToken);
        }

        var token = _jwtTokenService.GenerateToken(user);

        await _auditService.LogAsync(
            action: "LOGIN_SUCCESS",
            entityName: "User",
            entityId: user.Id.ToString(),
            username: user.Username,
            isSuccess: true,
            details: "User logged in successfully.");

        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == user.Id, cancellationToken);

        return new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            CustomerId = customer?.Id,
            DisplayName = customer is null
                ? string.Join(' ', new[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x)))
                : $"{customer.FirstName} {customer.LastName}"
        };
    }
}
