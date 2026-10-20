using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Delegations;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Infrastructure.Services;

public sealed class DelegationService : IDelegationService
{
    private readonly BankDbContext _context;
    private readonly IAuditService _audit;
    private readonly IApprovalAuthorizationService _authorization;

    public DelegationService(BankDbContext context, IAuditService audit, IApprovalAuthorizationService authorization)
    {
        _context = context;
        _audit = audit;
        _authorization = authorization;
    }

    public async Task<List<DelegationDto>> GetAllAsync(Guid? userId, CancellationToken cancellationToken = default)
    {
        var query = _context.ApprovalDelegations.AsNoTracking();
        if (userId is not null) query = query.Where(x => x.UserId == userId.Value);

        var rows = await query
            .Join(_context.Users.AsNoTracking(), d => d.UserId, u => u.Id,
                (d, u) => new { d, u.Username })
            .OrderBy(x => x.Username)
            .ThenBy(x => x.d.Operation)
            .ThenBy(x => x.d.Currency)
            .ToListAsync(cancellationToken);

        return rows.Select(x => Map(x.d, x.Username)).ToList();
    }

    public async Task<DelegationDto> CreateAsync(CreateDelegationRequest request, ActorContext owner, CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(owner, cancellationToken);
        var (operation, currency, maxAmount, dailyLimit) = Validate(
            request.Operation, request.Currency, request.MaxAmount, request.DailyLimit);

        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == request.UserId, cancellationToken)
                   ?? throw new NotFoundException("User", request.UserId.ToString());
        if (user.Role != UserRole.DelegatedApprover || user.Status != UserStatus.Active)
            throw new BusinessRuleException("Yetki yalnız aktif DelegatedApprover kullanıcısına verilebilir.");

        var exists = await _context.ApprovalDelegations.AnyAsync(x =>
            x.UserId == request.UserId && x.Operation == operation && x.Currency == currency && x.IsActive,
            cancellationToken);
        if (exists)
            throw new ConflictException("Bu kullanıcı/operasyon/para birimi için aktif bir yetki zaten var. Güncelleyin.");

        var entity = new ApprovalDelegation
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Operation = operation,
            Currency = currency,
            MaxAmount = maxAmount,
            DailyLimit = dailyLimit,
            IsActive = true,
            ExpiresAt = request.ExpiresAt,
            CreatedByOwnerId = owner.UserId,
            CreatedByOwnerUsername = owner.Username,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.ApprovalDelegations.Add(entity);
        _audit.Add("DELEGATION_CREATED", "ApprovalDelegation", entity.Id.ToString(), owner.Username, true,
            $"User: {user.Username}, Operation: {operation}, Currency: {currency ?? "n/a"}, " +
            $"MaxAmount: {maxAmount?.ToString("F2") ?? "n/a"}, DailyLimit: {dailyLimit?.ToString("F2") ?? "n/a"}");
        await _context.SaveChangesAsync(cancellationToken);
        return Map(entity, user.Username);
    }

    public async Task<DelegationDto> UpdateAsync(Guid id, UpdateDelegationRequest request, ActorContext owner, CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(owner, cancellationToken);
        var entity = await _context.ApprovalDelegations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException("ApprovalDelegation", id.ToString());

        var operation = request.Operation is null
            ? entity.Operation
            : ParseOperation(request.Operation);
        var currencyText = request.Currency ?? entity.Currency;
        var maxAmount = request.MaxAmount ?? entity.MaxAmount;
        var dailyLimit = request.DailyLimit ?? entity.DailyLimit;

        var (validatedOperation, currency, validatedMax, validatedDaily) =
            Validate(operation.ToString(), currencyText, maxAmount, dailyLimit);

        entity.Operation = validatedOperation;
        entity.Currency = currency;
        entity.MaxAmount = validatedMax;
        entity.DailyLimit = validatedDaily;
        entity.IsActive = request.IsActive ?? entity.IsActive;
        entity.ExpiresAt = request.ExpiresAt ?? entity.ExpiresAt;
        entity.UpdatedAt = DateTime.UtcNow;

        var username = await _context.Users.AsNoTracking()
            .Where(x => x.Id == entity.UserId).Select(x => x.Username).FirstAsync(cancellationToken);

        _audit.Add("DELEGATION_UPDATED", "ApprovalDelegation", entity.Id.ToString(), owner.Username, true,
            $"User: {username}, Operation: {entity.Operation}, Active: {entity.IsActive}, " +
            $"MaxAmount: {entity.MaxAmount?.ToString("F2") ?? "n/a"}, ExpiresAt: {entity.ExpiresAt:O}");
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Yetki kaydı başka bir Owner tarafından değiştirildi.");
        }

        return Map(entity, username);
    }

    public async Task DeactivateAsync(Guid id, ActorContext owner, CancellationToken cancellationToken = default)
    {
        await EnsureOwnerAsync(owner, cancellationToken);
        var entity = await _context.ApprovalDelegations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken)
                     ?? throw new NotFoundException("ApprovalDelegation", id.ToString());

        entity.IsActive = false;
        entity.UpdatedAt = DateTime.UtcNow;
        _audit.Add("DELEGATION_DEACTIVATED", "ApprovalDelegation", entity.Id.ToString(), owner.Username, true,
            $"Delegation revoked immediately. UserId: {entity.UserId}");
        await _context.SaveChangesAsync(cancellationToken);
    }

    private (ApprovalOperation Operation, string? Currency, decimal? MaxAmount, decimal? DailyLimit) Validate(
        string? operationText, string? currencyText, decimal? maxAmount, decimal? dailyLimit)
    {
        if (string.IsNullOrWhiteSpace(operationText)
            || !Enum.TryParse<ApprovalOperation>(operationText.Trim(), ignoreCase: true, out var operation))
            throw new BusinessRuleException("Geçersiz onay operasyonu.");

        if (operation == ApprovalOperation.ReconciliationException)
            return (operation, null, null, null);

        if (string.IsNullOrWhiteSpace(currencyText))
            throw new BusinessRuleException("Finansal yetki için para birimi zorunludur.");
        var currency = CurrencyCodes.Normalize(currencyText);

        if (maxAmount is null || maxAmount <= 0)
            throw new BusinessRuleException("İşlem başına üst limit pozitif olmalıdır.");
        Money.EnsureScale(maxAmount.Value, "Üst limit");

        if (dailyLimit is not null)
        {
            if (dailyLimit <= 0)
                throw new BusinessRuleException("Günlük limit pozitif olmalıdır.");
            Money.EnsureScale(dailyLimit.Value, "Günlük limit");
        }

        return (operation, currency, maxAmount, dailyLimit);
    }

    private async Task EnsureOwnerAsync(ActorContext actor, CancellationToken cancellationToken)
    {
        var active = await _authorization.RequireActiveActorAsync(actor.UserId, cancellationToken);
        if (active.Role != UserRole.Owner)
            throw new ForbiddenException("Yetki devri yalnız Owner tarafından yönetilebilir.");
    }

    private static ApprovalOperation ParseOperation(string value)
    {
        if (!Enum.TryParse<ApprovalOperation>(value.Trim(), ignoreCase: true, out var parsed))
            throw new BusinessRuleException("Geçersiz onay operasyonu.");
        return parsed;
    }

    private static DelegationDto Map(ApprovalDelegation d, string username) => new()
    {
        Id = d.Id,
        UserId = d.UserId,
        Username = username,
        Operation = d.Operation.ToString(),
        Currency = d.Currency,
        MaxAmount = d.MaxAmount,
        DailyLimit = d.DailyLimit,
        IsActive = d.IsActive,
        ExpiresAt = d.ExpiresAt,
        CreatedByOwnerUsername = d.CreatedByOwnerUsername,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt
    };
}
