using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Infrastructure.Services;

public sealed class ApprovalAuthorizationService : IApprovalAuthorizationService
{
    private readonly BankDbContext _context;

    public ApprovalAuthorizationService(BankDbContext context)
    {
        _context = context;
    }

    public async Task<ActorContext> RequireActiveActorAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken)
            ?? throw new UnauthorizedAccessException("User no longer exists.");

        if (user.Status != UserStatus.Active)
            throw new ForbiddenException("Hesabınız etkin değil. Owner aktivasyonu gerekir.");

        if (user.Role is not (UserRole.Owner or UserRole.DelegatedApprover or UserRole.Customer))
            throw new ForbiddenException("Bu hesap rolü artık geçerli değil; Owner ile iletişime geçin.");

        return new ActorContext(user.Id, user.Username, user.Role);
    }

    public async Task<bool> IsOwnerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.Users.AsNoTracking()
            .AnyAsync(x => x.Id == userId && x.Status == UserStatus.Active && x.Role == UserRole.Owner, cancellationToken);
    }

    public async Task<ApprovalPermission> CheckAsync(
        Guid userId,
        ApprovalOperation operation,
        string? currency,
        decimal? amount,
        CancellationToken cancellationToken = default)
    {
        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

        if (user is null || user.Status != UserStatus.Active)
            return ApprovalPermission.Deny("Hesabınız etkin değil.");

        if (user.Role == UserRole.Owner)
            return ApprovalPermission.Allow();

        if (user.Role != UserRole.DelegatedApprover)
            return ApprovalPermission.Deny("Bu işlem için onay yetkiniz yok.");

        var now = DateTime.UtcNow;
        var delegations = await _context.ApprovalDelegations.AsNoTracking()
            .Where(x => x.UserId == userId
                        && x.Operation == operation
                        && x.IsActive
                        && (x.ExpiresAt == null || x.ExpiresAt > now))
            .OrderByDescending(x => x.MaxAmount)
            .ToListAsync(cancellationToken);

        if (delegations.Count == 0)
            return ApprovalPermission.Deny("Bu operasyon için aktif bir yetki devriniz yok.");

        if (operation == ApprovalOperation.ReconciliationException)
            return ApprovalPermission.Allow(delegations[0].Id);

        if (string.IsNullOrWhiteSpace(currency))
            return ApprovalPermission.Deny("İşlem para birimi belirtilmemiş.");

        if (amount is null)
            return ApprovalPermission.Deny("İşlem tutarı belirtilmemiş.");

        foreach (var delegation in delegations)
        {
            if (!string.Equals(delegation.Currency, currency, StringComparison.OrdinalIgnoreCase))
                continue;

            if (delegation.MaxAmount is null || amount.Value > delegation.MaxAmount.Value)
                continue;

            if (delegation.DailyLimit is { } dailyLimit)
            {
                var used = await ApprovedTodayAsync(userId, operation, currency, cancellationToken);
                if (used + amount.Value > dailyLimit)
                    continue;
            }

            return ApprovalPermission.Allow(delegation.Id);
        }

        return ApprovalPermission.Deny(
            $"Bu işlem yetkinizin dışında (para birimi {currency}, tutar {amount.Value:N2}) veya günlük limit aşıldı. Owner onayı gerekir.");
    }

    private async Task<decimal> ApprovedTodayAsync(
        Guid userId, ApprovalOperation operation, string currency, CancellationToken cancellationToken)
    {
        var requestType = operation switch
        {
            ApprovalOperation.Transfer => ApprovalRequestType.Transfer,
            ApprovalOperation.Deposit => ApprovalRequestType.Deposit,
            ApprovalOperation.Withdrawal => ApprovalRequestType.Withdrawal,
            ApprovalOperation.AccountOpening => ApprovalRequestType.AccountOpening,
            _ => (ApprovalRequestType?)null
        };

        if (requestType is null) return 0m;

        var dayStart = DateTime.UtcNow.Date;
        var dayEnd = dayStart.AddDays(1);
        var values = await _context.PaymentApprovals.AsNoTracking()
            .Where(x => x.ReviewedByUserId == userId
                        && x.Status == PaymentApprovalStatus.Approved
                        && x.RequestType == requestType.Value
                        && x.Currency == currency
                        && x.ReviewedAt >= dayStart
                        && x.ReviewedAt < dayEnd)
            .Select(x => x.Amount)
            .ToListAsync(cancellationToken);

        return values.Sum();
    }
}
