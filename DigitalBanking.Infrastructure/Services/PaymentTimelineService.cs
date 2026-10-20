using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.CustomerPortal;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Projects real persisted approval/ledger events into a timeline and a demo
/// receipt. No stage is invented: legacy rows without events fall back only to
/// fields that actually exist (created/reviewed dates and related transaction).
/// </summary>
public sealed class PaymentTimelineService : IPaymentTimelineService
{
    private const string IstanbulTimeZoneId = "Europe/Istanbul";

    private readonly BankDbContext _context;

    public PaymentTimelineService(BankDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentTimelineDto> GetTimelineAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _context.PaymentApprovals
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null)
            throw new NotFoundException("Payment", paymentId.ToString());

        var events = await _context.PaymentTimelineEvents
            .AsNoTracking()
            .Where(x => x.PaymentApprovalId == paymentId)
            .OrderBy(x => x.OccurredAtUtc)
            .ThenBy(x => x.Id)
            .ToListAsync(cancellationToken);

        var timeZone = ResolveTimeZone();
        var dto = new PaymentTimelineDto
        {
            PaymentId = payment.Id,
            Reference = Masking.PaymentReference(payment.Id, payment.CorrelationId),
            CorrelationId = payment.CorrelationId,
            Status = payment.Status.ToString(),
            RiskLevel = payment.RiskLevel.ToString(),
            Amount = payment.Amount,
            Currency = payment.Currency,
            CreatedAtUtc = payment.CreatedAt,
            TimeZoneId = timeZone.Id
        };

        if (events.Count == 0)
        {
            dto.Events.Add(MakeEvent(PaymentTimelineEventType.Created, payment.CreatedAt, null, "System", "Payment request recorded.", true, timeZone));
            if (payment.ReviewedAt is { } reviewedAt)
            {
                dto.Events.Add(payment.Status == PaymentApprovalStatus.Rejected
                    ? MakeEvent(PaymentTimelineEventType.Rejected, reviewedAt, null, "Admin", "Payment rejected by checker.", false, timeZone)
                    : MakeEvent(PaymentTimelineEventType.Approved, reviewedAt, null, "Admin", "Payment approved by checker.", false, timeZone));
            }

            if (payment.RelatedTransactionId is not null)
                dto.Events.Add(MakeEvent(PaymentTimelineEventType.Completed, payment.ReviewedAt ?? payment.CreatedAt, null, "System", "Ledger movement completed.", true, timeZone));
        }
        else
        {
            foreach (var item in events)
            {
                dto.Events.Add(new PaymentTimelineEventDto
                {
                    Type = item.EventType.ToString(),
                    OccurredAtUtc = item.OccurredAtUtc,
                    OccurredAtLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(item.OccurredAtUtc, DateTimeKind.Utc), timeZone),
                    ActorUsername = item.ActorUsername,
                    ActorRole = item.ActorRole,
                    Description = item.Description,
                    IsSystem = item.IsSystem
                });
            }
        }

        return dto;
    }

    public async Task<PaymentReceiptDto> GetReceiptAsync(int paymentId, CancellationToken cancellationToken = default)
    {
        var payment = await _context.PaymentApprovals
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == paymentId, cancellationToken);

        if (payment is null)
            throw new NotFoundException("Payment", paymentId.ToString());

        var timeZone = ResolveTimeZone();
        var completed = payment.Status is (PaymentApprovalStatus.Approved or PaymentApprovalStatus.AutoApproved)
                        && payment.RelatedTransactionId is not null;

        var completedEvent = await _context.PaymentTimelineEvents
            .AsNoTracking()
            .Where(x => x.PaymentApprovalId == paymentId && x.EventType == PaymentTimelineEventType.Completed)
            .OrderByDescending(x => x.OccurredAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var completedAt = completedEvent?.OccurredAtUtc ?? payment.ReviewedAt ?? payment.CreatedAt;
        var fromAccount = await _context.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == payment.FromAccountId, cancellationToken);
        var toAccount = await _context.Accounts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == payment.ToAccountId, cancellationToken);

        return new PaymentReceiptDto
        {
            PaymentId = payment.Id,
            IsCompleted = completed,
            Reference = Masking.PaymentReference(payment.Id, payment.CorrelationId),
            Status = payment.Status.ToString(),
            StatusSummary = completed
                ? null
                : payment.Status == PaymentApprovalStatus.Rejected
                    ? "Talep reddedildi; tamamlanmış dekont üretilmez."
                    : "Talep henüz tamamlanmadı; tamamlanmış dekont üretilmez.",
            Amount = payment.Amount,
            Currency = payment.Currency,
            CompletedAtUtc = completed ? completedAt : null,
            CompletedAtLocal = completed
                ? TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(completedAt, DateTimeKind.Utc), timeZone)
                : null,
            TimeZoneId = timeZone.Id,
            FromAccountMasked = Masking.AccountNumber(fromAccount?.AccountNumber),
            ToAccountMasked = Masking.AccountNumber(toAccount?.AccountNumber),
            Description = payment.Description
        };
    }

    private static PaymentTimelineEventDto MakeEvent(
        PaymentTimelineEventType type,
        DateTime occurredAtUtc,
        string? actorUsername,
        string actorRole,
        string description,
        bool isSystem,
        TimeZoneInfo timeZone) => new()
    {
        Type = type.ToString(),
        OccurredAtUtc = DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc),
        OccurredAtLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(occurredAtUtc, DateTimeKind.Utc), timeZone),
        ActorUsername = actorUsername,
        ActorRole = actorRole,
        Description = description,
        IsSystem = isSystem
    };

    private static TimeZoneInfo ResolveTimeZone()
    {
        foreach (var id in new[] { IstanbulTimeZoneId, "Turkey Standard Time" })
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.Utc;
    }
}
