using System.Net.Mail;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Identity;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using DigitalBanking.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Infrastructure.Services;

public sealed class UserIdentityService : IUserIdentityService
{
    private readonly BankDbContext _context;
    private readonly PasswordHasher _passwordHasher;
    private readonly IAuditService _audit;
    private readonly IApprovalAuthorizationService _authorization;

    public UserIdentityService(
        BankDbContext context,
        PasswordHasher passwordHasher,
        IAuditService audit,
        IApprovalAuthorizationService authorization)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _audit = audit;
        _authorization = authorization;
    }

    public Task<bool> OwnerExistsAsync(CancellationToken cancellationToken = default) =>
        _context.Users.AsNoTracking().AnyAsync(x => x.Role == UserRole.Owner, cancellationToken);

    public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default)
    {
        var (username, email, firstName, lastName) = ValidateAndNormalize(
            request.Username, request.Email, request.FirstName, request.LastName);

        if (request.Password != request.ConfirmPassword)
            throw new BusinessRuleException("Şifre ve şifre doğrulaması eşleşmiyor.");
        PasswordPolicy.EnsureValid(request.Password);

        if (await _context.Users.AnyAsync(x => x.Username == username, cancellationToken))
            throw new ConflictException("Bu kullanıcı adı zaten kullanılıyor.");
        if (await _context.Users.AnyAsync(x => x.Email == email, cancellationToken))
            throw new ConflictException("Bu e-posta zaten kayıtlı.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.Customer,
            Status = UserStatus.PendingActivation,
            CreatedDate = DateTime.UtcNow
        };
        _context.Users.Add(user);
        _audit.Add("MEMBERSHIP_REGISTERED", "User", user.Id.ToString(), username, true,
            "Membership application created with PendingActivation. No role/limit/ownership supplied by client.");
        await _context.SaveChangesAsync(cancellationToken);

        return new RegisterResponse
        {
            UserId = user.Id,
            Status = user.Status.ToString(),
            Message = "Üyelik başvurusu alındı. Demo KYC yoktur; Owner aktivasyonu sonrası giriş yapılabilir."
        };
    }

    public async Task<UserAdminDto> SetupOwnerAsync(SetupOwnerRequest request, CancellationToken cancellationToken = default)
    {
        if (await OwnerExistsAsync(cancellationToken))
            throw new ConflictException("Owner zaten kurulmuş. Kurulum endpoint'i kapatıldı.");

        var (username, email, firstName, lastName) = ValidateAndNormalize(
            request.Username, request.Email, request.FirstName, request.LastName);

        if (request.Password != request.ConfirmPassword)
            throw new BusinessRuleException("Şifre ve şifre doğrulaması eşleşmiyor.");
        PasswordPolicy.EnsureValid(request.Password);

        if (await _context.Users.AnyAsync(x => x.Username == username, cancellationToken))
            throw new ConflictException("Bu kullanıcı adı zaten kullanılıyor.");
        if (await _context.Users.AnyAsync(x => x.Email == email, cancellationToken))
            throw new ConflictException("Bu e-posta zaten kayıtlı.");

        var owner = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.Owner,
            Status = UserStatus.Active,
            CreatedDate = DateTime.UtcNow,
            ActivatedAt = DateTime.UtcNow
        };
        _context.Users.Add(owner);
        _audit.Add("OWNER_SETUP", "User", owner.Id.ToString(), owner.Username, true,
            "First Owner created through localhost setup; endpoint is now closed.");
        await _context.SaveChangesAsync(cancellationToken);
        return Map(owner);
    }

    public async Task<List<UserAdminDto>> GetUsersAsync(string? status, string? role, CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<UserStatus>(status, ignoreCase: true, out var parsedStatus))
                throw new BusinessRuleException("Geçersiz kullanıcı durumu.");
            query = query.Where(x => x.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(role))
        {
            if (!Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsedRole))
                throw new BusinessRuleException("Geçersiz rol.");
            query = query.Where(x => x.Role == parsedRole);
        }

        var users = await query.OrderByDescending(x => x.CreatedDate).ToListAsync(cancellationToken);
        return users.Select(Map).ToList();
    }

    public async Task<UserAdminDto> ActivateAsync(Guid userId, ActorContext owner, CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(owner, cancellationToken);
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                   ?? throw new NotFoundException("User", userId.ToString());

        if (user.Role == UserRole.Owner)
            throw new BusinessRuleException("Owner aktivasyonu bu endpoint ile değiştirilemez.");
        if (user.Status == UserStatus.Active)
            return Map(user);

        user.Status = UserStatus.Active;
        user.ActivatedAt = DateTime.UtcNow;
        user.ActivatedByUserId = owner.UserId;

        if (user.Role == UserRole.Customer)
            await EnsureCustomerProfileAsync(user, cancellationToken);

        _audit.Add("MEMBERSHIP_ACTIVATED", "User", user.Id.ToString(), owner.Username, true,
            $"Status -> Active. Role: {user.Role}");
        await _context.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task<UserAdminDto> DisableAsync(Guid userId, ActorContext owner, CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(owner, cancellationToken);
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                   ?? throw new NotFoundException("User", userId.ToString());

        if (user.Role == UserRole.Owner)
            throw new BusinessRuleException("Owner devre dışı bırakılamaz; son Owner koruması gereği bu işlem reddedildi.");

        user.Status = UserStatus.Disabled;
        var delegations = await _context.ApprovalDelegations.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
        foreach (var delegation in delegations)
        {
            delegation.IsActive = false;
            delegation.UpdatedAt = DateTime.UtcNow;
        }

        _audit.Add("MEMBERSHIP_DISABLED", "User", user.Id.ToString(), owner.Username, true,
            $"Status -> Disabled. Delegations deactivated: {delegations.Count}");
        await _context.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    public async Task<UserAdminDto> ChangeRoleAsync(Guid userId, string role, ActorContext owner, CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(owner, cancellationToken);
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
                   ?? throw new NotFoundException("User", userId.ToString());

        if (user.Role == UserRole.Owner)
            throw new BusinessRuleException("Owner rolü değiştirilemez; son Owner koruması gereği bu işlem reddedildi.");

        if (!TryParseNonOwnerRole(role, out var newRole))
            throw new BusinessRuleException("Yalnız Customer veya DelegatedApprover rolü atanabilir. Owner atanamaz.");
        if (user.Status != UserStatus.Active)
            throw new BusinessRuleException("Rol değişikliği için kullanıcı aktif olmalıdır.");

        var oldRole = user.Role;
        user.Role = newRole;

        if (newRole == UserRole.Customer)
        {
            await EnsureCustomerProfileAsync(user, cancellationToken);
            var delegations = await _context.ApprovalDelegations.Where(x => x.UserId == userId).ToListAsync(cancellationToken);
            foreach (var delegation in delegations)
            {
                delegation.IsActive = false;
                delegation.UpdatedAt = DateTime.UtcNow;
            }
        }

        _audit.Add("USER_ROLE_CHANGED", "User", user.Id.ToString(), owner.Username, true,
            $"Role: {oldRole} -> {newRole}");
        await _context.SaveChangesAsync(cancellationToken);
        return Map(user);
    }

    private async Task EnsureOwnerAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        var active = await _authorization.RequireActiveActorAsync(actor.UserId, cancellationToken);
        if (active.Role != UserRole.Owner)
            throw new ForbiddenException("Bu işlem yalnız Owner tarafından yapılabilir.");
    }

    private async Task EnsureCustomerProfileAsync(User user, CancellationToken cancellationToken)
    {
        var exists = await _context.Customers.AnyAsync(x => x.UserId == user.Id, cancellationToken);
        if (exists) return;

        _context.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FirstName = string.IsNullOrWhiteSpace(user.FirstName) ? user.Username : user.FirstName,
            LastName = string.IsNullOrWhiteSpace(user.LastName) ? "Üye" : user.LastName,
            IdentityNumber = "MEM-" + user.Id.ToString("N")[..12].ToUpperInvariant(),
            IsDemo = false,
            CreatedDate = DateTime.UtcNow
        });
    }

    private static (string Username, string Email, string FirstName, string LastName) ValidateAndNormalize(
        string? username, string? email, string? firstName, string? lastName)
    {
        var cleanUsername = (username ?? string.Empty).Trim();
        var cleanEmail = (email ?? string.Empty).Trim().ToLowerInvariant();
        var cleanFirst = (firstName ?? string.Empty).Trim();
        var cleanLast = (lastName ?? string.Empty).Trim();

        if (cleanUsername.Length < 3 || cleanUsername.Length > 64)
            throw new BusinessRuleException("Kullanıcı adı 3–64 karakter olmalıdır.");
        if (cleanFirst.Length is < 1 or > 100 || cleanLast.Length is < 1 or > 100)
            throw new BusinessRuleException("Ad ve soyad zorunludur (en fazla 100 karakter).");
        if (cleanEmail.Length > 256 || !IsValidEmail(cleanEmail))
            throw new BusinessRuleException("Geçerli bir e-posta adresi giriniz.");

        return (cleanUsername, cleanEmail, cleanFirst, cleanLast);
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var address = new MailAddress(email);
            return string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool TryParseNonOwnerRole(string? role, out UserRole parsed)
    {
        parsed = UserRole.None;
        if (string.IsNullOrWhiteSpace(role)) return false;
        var normalized = role.Trim();
        if (string.Equals(normalized, nameof(UserRole.DelegatedApprover), StringComparison.OrdinalIgnoreCase))
        {
            parsed = UserRole.DelegatedApprover;
            return true;
        }
        if (string.Equals(normalized, nameof(UserRole.Customer), StringComparison.OrdinalIgnoreCase))
        {
            parsed = UserRole.Customer;
            return true;
        }
        return false;
    }

    private static UserAdminDto Map(User user) => new()
    {
        Id = user.Id,
        Username = user.Username,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        Role = user.Role.ToString(),
        Status = user.Status.ToString(),
        CreatedDate = user.CreatedDate,
        ActivatedAt = user.ActivatedAt
    };
}
