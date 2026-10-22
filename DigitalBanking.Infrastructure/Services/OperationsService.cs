using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Operations;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Operations control centre. Owner sees the whole pending queue; a delegated
/// approver only sees requests their live delegation permits and is never shown
/// their own request. Ages use an injected UTC clock, and assignment is an
/// operational hint only: approval still re-checks role/delegation/limit.
/// </summary>
public class OperationsService : IOperationsService
{
    public const int MaxPageSize = 50;
    private const int MaxPendingScan = 1000;
    private static readonly TimeZoneInfo Istanbul = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    private readonly BankDbContext _context;
    private readonly IApprovalAuthorizationService _authorization;
    private readonly IAuditService _audit;
    private readonly IClock _clock;
    private readonly OperationsOptions _options;

    public OperationsService(
        BankDbContext context,
        IApprovalAuthorizationService authorization,
        IAuditService audit,
        IClock clock,
        IOptions<OperationsOptions> options)
    {
        _context = context;
        _authorization = authorization;
        _audit = audit;
        _clock = clock;
        _options = options.Value;
    }

    public async Task<OperationsSummaryDto> GetSummaryAsync(
        ActorContext actor, CancellationToken cancellationToken = default)
    {
        var dbActor = await RequireOperationsActorAsync(actor, cancellationToken);
        var now = _clock.UtcNow;
        var sla = SlaMinutes;
        var pending = await LoadVisiblePendingAsync(dbActor, cancellationToken);

        var canViewReconciliation = await CanViewReconciliationAsync(dbActor, cancellationToken);
        var unresolvedBatches = 0;
        var openExceptions = 0;
        if (canViewReconciliation)
        {
            unresolvedBatches = (await LoadReconciliationDifferencesAsync(null, "open", cancellationToken)).Count;
            openExceptions = await _context.ReconciliationExceptions.AsNoTracking()
                .CountAsync(x => x.Status != ReconciliationExceptionStatus.Closed
                                 && x.Status != ReconciliationExceptionStatus.Rejected, cancellationToken);
        }

        return new OperationsSummaryDto
        {
            GeneratedAtUtc = now,
            GeneratedAtIstanbul = ToIstanbul(now),
            PendingSlaMinutes = sla,
            PendingCount = pending.Count,
            OverdueCount = pending.Count(x => AgeMinutes(x.CreatedAt, now) >= sla),
            UnassignedCount = pending.Count(x => x.AssignedToUserId is null),
            AssignedToMeCount = pending.Count(x => x.AssignedToUserId == dbActor.UserId),
            OldestPendingAgeMinutes = pending.Count == 0 ? 0 : pending.Max(x => AgeMinutes(x.CreatedAt, now)),
            CanViewReconciliation = canViewReconciliation,
            UnresolvedReconciliationBatchCount = unresolvedBatches,
            OpenReconciliationExceptionCount = openExceptions
        };
    }

    public async Task<PaginatedResponse<OperationsApprovalRowDto>> GetApprovalsAsync(
        int page, int pageSize, string? status, string? requestType, string? currency,
        string? age, string? assignment, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var dbActor = await RequireOperationsActorAsync(actor, cancellationToken);
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var parsedStatus = ParseStatus(status);
        if (dbActor.Role == UserRole.DelegatedApprover && parsedStatus != PaymentApprovalStatus.Pending)
            throw new ForbiddenException("DelegatedApprover operasyon merkezinde yalnız bekleyen talepleri görebilir.");

        var parsedAge = ParseAgeFilter(age);
        if (parsedAge != AgeFilter.All && parsedStatus != PaymentApprovalStatus.Pending)
            throw new BusinessRuleException("Yaş/SLA filtresi yalnız bekleyen talepler için kullanılabilir.");

        var parsedAssignment = ParseAssignmentFilter(assignment);
        var parsedType = ParseRequestType(requestType);
        var normalizedCurrency = string.IsNullOrWhiteSpace(currency) ? null : CurrencyCodes.Normalize(currency);

        List<PaymentApproval> candidates;
        if (parsedStatus == PaymentApprovalStatus.Pending)
        {
            candidates = await LoadVisiblePendingAsync(dbActor, cancellationToken);
        }
        else
        {
            candidates = await _context.PaymentApprovals.AsNoTracking()
                .Where(x => x.Status == parsedStatus)
                .OrderBy(x => x.CreatedAt)
                .Take(MaxPendingScan)
                .ToListAsync(cancellationToken);
        }

        var now = _clock.UtcNow;
        var sla = SlaMinutes;

        var filtered = candidates.Where(x =>
        {
            if (parsedType is not null && x.RequestType != parsedType.Value) return false;
            if (normalizedCurrency is not null && x.Currency != normalizedCurrency) return false;

            if (parsedStatus == PaymentApprovalStatus.Pending)
            {
                var rowAge = AgeMinutes(x.CreatedAt, now);
                if (parsedAge == AgeFilter.Overdue && rowAge < sla) return false;
                if (parsedAge == AgeFilter.Within && rowAge >= sla) return false;
            }

            if (parsedAssignment == AssignmentFilter.Unassigned && x.AssignedToUserId is not null) return false;
            if (parsedAssignment == AssignmentFilter.Mine && x.AssignedToUserId != dbActor.UserId) return false;
            return true;
        }).ToList();

        var total = filtered.Count;
        var pageItems = filtered
            .OrderBy(x => x.CreatedAt)
            .ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var userIds = pageItems.Select(x => x.RequestedByUserId)
            .Concat(pageItems.Where(x => x.AssignedToUserId is not null).Select(x => x.AssignedToUserId!.Value))
            .Distinct()
            .ToList();
        var usernames = await _context.Users.AsNoTracking()
            .Where(x => userIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Username })
            .ToDictionaryAsync(x => x.Id, x => x.Username, cancellationToken);

        var items = new List<OperationsApprovalRowDto>(pageItems.Count);
        foreach (var approval in pageItems)
        {
            var row = new OperationsApprovalRowDto
            {
                Id = approval.Id,
                RequestType = approval.RequestType.ToString(),
                RequestTypeLabel = RequestTypeLabel(approval.RequestType),
                Description = approval.Description,
                Amount = approval.Amount,
                Currency = approval.Currency,
                Status = approval.Status.ToString(),
                RiskLevel = approval.RiskLevel.ToString(),
                RequestedByUserId = approval.RequestedByUserId,
                RequestedByUsername = usernames.GetValueOrDefault(approval.RequestedByUserId),
                CreatedAt = approval.CreatedAt,
                AgeMinutes = AgeMinutes(approval.CreatedAt, now),
                SlaMinutes = sla,
                IsOverdue = approval.Status == PaymentApprovalStatus.Pending
                            && AgeMinutes(approval.CreatedAt, now) >= sla,
                AssignedToUserId = approval.AssignedToUserId,
                AssignedToUsername = approval.AssignedToUsername,
                AssignedAt = approval.AssignedAt,
                IsAssignedToMe = approval.AssignedToUserId == dbActor.UserId
            };

            if (approval.Status == PaymentApprovalStatus.Pending)
            {
                row.Assignees = await LoadAssigneesAsync(approval, dbActor, cancellationToken);
                row.CanAssign = dbActor.Role == UserRole.Owner || row.Assignees.Any(x => x.IsCurrentUser);
            }

            items.Add(row);
        }

        return new PaginatedResponse<OperationsApprovalRowDto>(items, page, pageSize, total);
    }

    public async Task<PaginatedResponse<OperationsReconciliationItemDto>> GetReconciliationAsync(
        int page, int pageSize, string? currency, string? exceptionFilter,
        ActorContext actor, CancellationToken cancellationToken = default)
    {
        var dbActor = await RequireOperationsActorAsync(actor, cancellationToken);
        if (!await CanViewReconciliationAsync(dbActor, cancellationToken))
            throw new ForbiddenException("Mutabakat farkları için aktif istisna yetkiniz yok.");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        var normalized = string.IsNullOrWhiteSpace(currency) ? null : CurrencyCodes.Normalize(currency);
        var all = await LoadReconciliationDifferencesAsync(normalized, exceptionFilter, cancellationToken);
        var items = all.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return new PaginatedResponse<OperationsReconciliationItemDto>(items, page, pageSize, all.Count);
    }

    public async Task<OperationsAssignmentResultDto> AssignApprovalAsync(
        int approvalId, Guid? assignedToUserId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var dbActor = await RequireOperationsActorAsync(actor, cancellationToken);
        var approval = await _context.PaymentApprovals
            .FirstOrDefaultAsync(x => x.Id == approvalId, cancellationToken)
            ?? throw new NotFoundException("Payment", approvalId.ToString());

        if (approval.Status != PaymentApprovalStatus.Pending)
            throw new BusinessRuleException("Yalnız bekleyen talep atanabilir.");
        if (dbActor.Role == UserRole.DelegatedApprover && approval.RequestedByUserId == dbActor.UserId)
            throw new ForbiddenException("Kendi talebinizi atayamazsınız.");

        if (assignedToUserId is { } targetId)
        {
            if (targetId == approval.RequestedByUserId)
                throw new BusinessRuleException("Talebi oluşturan kullanıcıya atama yapılamaz.");
            if (dbActor.Role == UserRole.DelegatedApprover && targetId != dbActor.UserId)
                throw new ForbiddenException("DelegatedApprover yalnız kendine atama yapabilir.");

            var target = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == targetId, cancellationToken)
                ?? throw new BusinessRuleException("Atanacak kullanıcı bulunamadı.");
            if (target.Status != UserStatus.Active)
                throw new BusinessRuleException("Pasif kullanıcıya atama yapılamaz.");
            if (target.Role is not (UserRole.Owner or UserRole.DelegatedApprover))
                throw new BusinessRuleException("Atama yalnız aktif Owner veya DelegatedApprover kullanıcısına yapılabilir.");

            var permission = await _authorization.CheckAsync(
                target.Id, OperationFor(approval.RequestType), approval.Currency, approval.Amount, cancellationToken);
            if (!permission.Allowed)
                throw new BusinessRuleException(
                    $"Atanacak kullanıcı bu talebi onaylayamaz: {permission.Reason}");
        }
        else if (dbActor.Role == UserRole.DelegatedApprover && approval.AssignedToUserId != dbActor.UserId)
        {
            throw new ForbiddenException("Yalnız kendi atamanızı kaldırabilirsiniz.");
        }

        if (approval.AssignedToUserId == assignedToUserId)
        {
            return new OperationsAssignmentResultDto
            {
                ApprovalId = approval.Id,
                AssignedToUserId = approval.AssignedToUserId,
                AssignedToUsername = approval.AssignedToUsername,
                Changed = false,
                Message = "Atama zaten aynı kullanıcıda; değişiklik yapılmadı."
            };
        }

        var now = _clock.UtcNow;
        var previous = approval.AssignedToUsername;
        string? username = null;
        if (assignedToUserId is not null)
        {
            username = await _context.Users.AsNoTracking()
                .Where(x => x.Id == assignedToUserId.Value)
                .Select(x => x.Username)
                .FirstOrDefaultAsync(cancellationToken);
        }

        approval.AssignedToUserId = assignedToUserId;
        approval.AssignedToUsername = username;
        approval.AssignedAt = assignedToUserId is null ? null : now;
        _audit.Add("APPROVAL_ASSIGNED", "PaymentApproval", approval.Id.ToString(), dbActor.Username, true,
            $"From: {previous ?? "none"}, To: {username ?? "none"}. Assignment does not grant approval rights.");

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyException("Talep başka bir kullanıcı tarafından güncellendi; atama tekrar denenmeli.");
        }

        return new OperationsAssignmentResultDto
        {
            ApprovalId = approval.Id,
            AssignedToUserId = approval.AssignedToUserId,
            AssignedToUsername = approval.AssignedToUsername,
            Changed = true,
            Message = username is null ? "Atama kaldırıldı." : $"Talep {username} kullanıcısına atandı."
        };
    }

    private async Task<List<PaymentApproval>> LoadVisiblePendingAsync(
        ActorContext dbActor, CancellationToken cancellationToken)
    {
        var candidate = await _context.PaymentApprovals.AsNoTracking()
            .Where(x => x.Status == PaymentApprovalStatus.Pending)
            .OrderBy(x => x.CreatedAt)
            .Take(MaxPendingScan)
            .ToListAsync(cancellationToken);

        if (dbActor.Role == UserRole.Owner)
            return candidate;

        var visible = new List<PaymentApproval>();
        foreach (var item in candidate)
        {
            if (item.RequestedByUserId == dbActor.UserId) continue;
            var permission = await _authorization.CheckAsync(
                dbActor.UserId, OperationFor(item.RequestType), item.Currency, item.Amount, cancellationToken);
            if (permission.Allowed) visible.Add(item);
        }
        return visible;
    }

    private async Task<List<OperationsAssigneeDto>> LoadAssigneesAsync(
        PaymentApproval approval, ActorContext dbActor, CancellationToken cancellationToken)
    {
        var candidates = await _context.Users.AsNoTracking()
            .Where(x => x.Status == UserStatus.Active
                        && (x.Role == UserRole.Owner || x.Role == UserRole.DelegatedApprover)
                        && x.Id != approval.RequestedByUserId
                        && (dbActor.Role == UserRole.Owner || x.Id == dbActor.UserId))
            .OrderBy(x => x.Role)
            .ThenBy(x => x.Username)
            .ToListAsync(cancellationToken);

        var result = new List<OperationsAssigneeDto>();
        foreach (var user in candidates)
        {
            var permission = await _authorization.CheckAsync(
                user.Id, OperationFor(approval.RequestType), approval.Currency, approval.Amount, cancellationToken);
            if (!permission.Allowed) continue;
            result.Add(new OperationsAssigneeDto
            {
                UserId = user.Id,
                Username = user.Username,
                Role = user.Role.ToString(),
                IsCurrentUser = user.Id == dbActor.UserId
            });
        }
        return result;
    }

    private async Task<bool> CanViewReconciliationAsync(
        ActorContext dbActor, CancellationToken cancellationToken)
    {
        if (dbActor.Role == UserRole.Owner) return true;
        var permission = await _authorization.CheckAsync(
            dbActor.UserId, ApprovalOperation.ReconciliationException, null, null, cancellationToken);
        return permission.Allowed;
    }

    private async Task<List<OperationsReconciliationItemDto>> LoadReconciliationDifferencesAsync(
        string? currency, string? exceptionFilter, CancellationToken cancellationToken)
    {
        var filter = string.IsNullOrWhiteSpace(exceptionFilter)
            ? "open"
            : exceptionFilter.Trim().ToLowerInvariant();
        if (filter is not ("open" or "none" or "all"))
            throw new BusinessRuleException("Geçersiz mutabakat farkı filtresi.");

        var query = _context.ReconciliationBatches.AsNoTracking()
            .Where(x => x.Status == ReconciliationBatchStatus.Imported
                        && (x.OnlyExternalCount + x.OnlySystemCount + x.AmountMismatchCount
                            + x.CurrencyMismatchCount + x.DuplicateReferenceCount) > 0);
        if (currency is not null)
            query = query.Where(x => x.CurrencyScope == currency);

        var rows = await query
            .OrderByDescending(x => x.CreatedAt)
            .Select(x => new
            {
                x.Id,
                x.FileName,
                x.CurrencyScope,
                x.CreatedAt,
                x.RowCount,
                x.OnlyExternalCount,
                x.OnlySystemCount,
                x.AmountMismatchCount,
                x.CurrencyMismatchCount,
                x.DuplicateReferenceCount,
                ExceptionCount = x.Exceptions.Count,
                UnresolvedExceptionCount = x.Exceptions.Count(e =>
                    e.Status != ReconciliationExceptionStatus.Closed
                    && e.Status != ReconciliationExceptionStatus.Rejected)
            })
            .ToListAsync(cancellationToken);

        var now = _clock.UtcNow;
        var items = new List<OperationsReconciliationItemDto>();
        foreach (var row in rows)
        {
            var include = filter switch
            {
                "none" => row.ExceptionCount == 0,
                "all" => true,
                _ => row.ExceptionCount == 0 || row.UnresolvedExceptionCount > 0
            };
            if (!include) continue;

            items.Add(new OperationsReconciliationItemDto
            {
                BatchId = row.Id,
                FileName = row.FileName,
                CurrencyScope = row.CurrencyScope,
                CreatedAt = row.CreatedAt,
                AgeMinutes = AgeMinutes(row.CreatedAt, now),
                RowCount = row.RowCount,
                DifferenceCount = row.OnlyExternalCount + row.OnlySystemCount + row.AmountMismatchCount
                                  + row.CurrencyMismatchCount + row.DuplicateReferenceCount,
                OnlyExternalCount = row.OnlyExternalCount,
                OnlySystemCount = row.OnlySystemCount,
                AmountMismatchCount = row.AmountMismatchCount,
                CurrencyMismatchCount = row.CurrencyMismatchCount,
                DuplicateReferenceCount = row.DuplicateReferenceCount,
                ExceptionCount = row.ExceptionCount,
                UnresolvedExceptionCount = row.UnresolvedExceptionCount,
                ResolutionState = row.ExceptionCount == 0 ? "NoException" : "Open"
            });
        }
        return items;
    }

    private async Task<ActorContext> RequireOperationsActorAsync(
        ActorContext actor, CancellationToken cancellationToken)
    {
        var dbActor = await _authorization.RequireActiveActorAsync(actor.UserId, cancellationToken);
        if (dbActor.Role is not (UserRole.Owner or UserRole.DelegatedApprover))
            throw new ForbiddenException("Operasyon merkezi yalnız Owner veya DelegatedApprover tarafından görülebilir.");
        return dbActor;
    }

    private int SlaMinutes => Math.Clamp(_options.PendingSlaMinutes, 1, 10_080);

    private static int AgeMinutes(DateTime createdAt, DateTime now)
    {
        var age = now - createdAt;
        return age <= TimeSpan.Zero ? 0 : (int)Math.Floor(age.TotalMinutes);
    }

    private static DateTime ToIstanbul(DateTime utc)
    {
        var specified = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(specified, Istanbul);
    }

    private static PaymentApprovalStatus ParseStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return PaymentApprovalStatus.Pending;
        if (!Enum.TryParse<PaymentApprovalStatus>(status, ignoreCase: true, out var parsed)
            || parsed == PaymentApprovalStatus.AutoApproved)
            throw new BusinessRuleException("Geçersiz talep durumu.");
        return parsed;
    }

    private static ApprovalRequestType? ParseRequestType(string? requestType)
    {
        if (string.IsNullOrWhiteSpace(requestType)) return null;
        if (!Enum.TryParse<ApprovalRequestType>(requestType, ignoreCase: true, out var parsed))
            throw new BusinessRuleException("Geçersiz talep türü.");
        return parsed;
    }

    private static AgeFilter ParseAgeFilter(string? age) => (age ?? "all").Trim().ToLowerInvariant() switch
    {
        "all" => AgeFilter.All,
        "overdue" => AgeFilter.Overdue,
        "within" => AgeFilter.Within,
        _ => throw new BusinessRuleException("Geçersiz yaş filtresi (all|overdue|within).")
    };

    private static AssignmentFilter ParseAssignmentFilter(string? assignment) =>
        (assignment ?? "all").Trim().ToLowerInvariant() switch
        {
            "all" => AssignmentFilter.All,
            "unassigned" => AssignmentFilter.Unassigned,
            "me" => AssignmentFilter.Mine,
            _ => throw new BusinessRuleException("Geçersiz atama filtresi (all|unassigned|me).")
        };

    private static ApprovalOperation OperationFor(ApprovalRequestType type) => type switch
    {
        ApprovalRequestType.Transfer => ApprovalOperation.Transfer,
        ApprovalRequestType.Deposit => ApprovalOperation.Deposit,
        ApprovalRequestType.Withdrawal => ApprovalOperation.Withdrawal,
        ApprovalRequestType.AccountOpening => ApprovalOperation.AccountOpening,
        _ => throw new BusinessRuleException("Geçersiz talep türü.")
    };

    private static string RequestTypeLabel(ApprovalRequestType type) => type switch
    {
        ApprovalRequestType.Transfer => "Transfer",
        ApprovalRequestType.Deposit => "Yatırma",
        ApprovalRequestType.Withdrawal => "Çekme",
        ApprovalRequestType.AccountOpening => "Hesap açılışı",
        _ => type.ToString()
    };

    private enum AgeFilter { All, Overdue, Within }
    private enum AssignmentFilter { All, Unassigned, Mine }
}
