using System.Security.Cryptography;
using AutoMapper;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.ApprovalRequests;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Owner/DelegatedApprover/Customer approval workflow.
///
/// Every financial request is Pending; nothing moves money before approval.
/// Owner may approve anything (except own request); DelegatedApprover is checked
/// against the live delegation row (operation, currency, per-request cap, daily
/// cap, expiry, active flag) on every approval. Revocation therefore applies even
/// to old JWTs/open screens. Account opening/deposit/withdrawal are request types.
/// </summary>
public class PaymentApprovalService : IPaymentApprovalService
{
    private readonly BankDbContext _context;
    private readonly IMapper _mapper;
    private readonly IAuditService _auditService;
    private readonly ICustomerAccessService _customerAccess;
    private readonly IApprovalAuthorizationService _authorization;
    private readonly ILogger _logger;

    public PaymentApprovalService(
        BankDbContext context,
        IMapper mapper,
        IAuditService auditService,
        ICustomerAccessService customerAccess,
        IApprovalAuthorizationService authorization)
    {
        _context = context;
        _mapper = mapper;
        _auditService = auditService;
        _customerAccess = customerAccess;
        _authorization = authorization;
        _logger = Log.ForContext<PaymentApprovalService>();
    }

    public Task<PaymentApprovalResponse> CreateApprovalRequestAsync(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string? description,
        ActorContext actor,
        bool isDemo = false,
        CancellationToken cancellationToken = default) =>
        CreateInternalAsync(
            new CreateOperationalRequest
            {
                RequestType = ApprovalRequestType.Transfer.ToString(),
                AccountId = fromAccountId,
                ToAccountId = toAccountId,
                Amount = amount,
                Description = description
            },
            actor, requireOwner: true, isDemo, cancellationToken);

    public Task<PaymentApprovalResponse> CreateCustomerTransferRequestAsync(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string? description,
        ActorContext actor,
        CancellationToken cancellationToken = default) =>
        CreateInternalAsync(
            new CreateOperationalRequest
            {
                RequestType = ApprovalRequestType.Transfer.ToString(),
                AccountId = fromAccountId,
                ToAccountId = toAccountId,
                Amount = amount,
                Description = description
            },
            actor, requireOwner: false, isDemo: false, cancellationToken);

    public Task<PaymentApprovalResponse> CreateCustomerOperationRequestAsync(
        CreateCustomerOperationRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default) =>
        CreateInternalAsync(
            new CreateOperationalRequest
            {
                RequestType = request.RequestType,
                AccountId = request.AccountId,
                AccountName = request.AccountName,
                AccountType = request.AccountType,
                Currency = request.Currency,
                Amount = request.Amount,
                Description = request.Description
            },
            actor, requireOwner: false, isDemo: false, cancellationToken);

    public Task<PaymentApprovalResponse> CreateOperationalRequestAsync(
        CreateOperationalRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default) =>
        CreateInternalAsync(request, actor, requireOwner: true, isDemo: false, cancellationToken);

    private async Task<PaymentApprovalResponse> CreateInternalAsync(
        CreateOperationalRequest request,
        ActorContext actor,
        bool requireOwner,
        bool isDemo,
        CancellationToken cancellationToken)
    {
        var dbActor = await _authorization.RequireActiveActorAsync(actor.UserId, cancellationToken);
        if (requireOwner && dbActor.Role != UserRole.Owner)
            throw new ForbiddenException("Bu operasyonel talebi yalnız Owner oluşturabilir.");
        if (!requireOwner && dbActor.Role != UserRole.Customer)
            throw new ForbiddenException("Müşteri talebi yalnız Customer rolüyle oluşturulabilir.");

        var requestType = ParseRequestType(request.RequestType);
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        if (description is { Length: > 250 })
            throw new BusinessRuleException("Açıklama 250 karakteri aşamaz.");

        Guid? customerIdForOpening = request.CustomerId;
        if (!requireOwner)
        {
            customerIdForOpening = await _customerAccess.GetCustomerIdAsync(dbActor.UserId, cancellationToken)
                ?? throw new ForbiddenException("Bu kullanıcıya bağlı müşteri profili bulunamadı.");
        }

        Account? account = null;
        Account? toAccount = null;
        string currency;
        AccountType? requestedAccountType = null;
        string? requestedAccountName = null;

        if (requestType == ApprovalRequestType.AccountOpening)
        {
            Money.EnsureNonNegative(request.Amount, "Açılış bakiyesi");
            currency = CurrencyCodes.Normalize(request.Currency);
            requestedAccountType = AccountMetadata.ParseType(request.AccountType);
            requestedAccountName = AccountMetadata.NormalizeName(request.AccountName, requestedAccountType.Value);
            if (customerIdForOpening is null
                || !await _context.Customers.AnyAsync(x => x.Id == customerIdForOpening.Value, cancellationToken))
                throw new NotFoundException("Customer", customerIdForOpening?.ToString() ?? string.Empty);
        }
        else
        {
            if (request.AccountId is null)
                throw new BusinessRuleException("Hesap seçimi zorunludur.");
            account = await _context.Accounts.FirstOrDefaultAsync(x => x.Id == request.AccountId.Value, cancellationToken)
                ?? throw new NotFoundException("Account", request.AccountId.Value.ToString());

            if (!requireOwner && !await _customerAccess.UserOwnsAccountAsync(dbActor.UserId, account.Id, cancellationToken))
                throw new NotFoundException("Account", account.Id.ToString());

            currency = account.Currency;
            Money.EnsurePositive(request.Amount);

            if (requestType is ApprovalRequestType.Deposit)
            {
                // Deposit is a request too; no balance change before approval.
            }
            else if (requestType is ApprovalRequestType.Withdrawal)
            {
                await EnsureAvailableBalanceAsync(account.Id, request.Amount, cancellationToken);
            }
            else
            {
                if (request.ToAccountId is null || request.ToAccountId == account.Id)
                    throw new BusinessRuleException("Kaynak ve hedef hesap farklı olmalıdır.");
                toAccount = await _context.Accounts.FirstOrDefaultAsync(x => x.Id == request.ToAccountId.Value, cancellationToken)
                    ?? throw new NotFoundException("Account", request.ToAccountId.Value.ToString());
                if (!string.Equals(account.Currency, toAccount.Currency, StringComparison.OrdinalIgnoreCase))
                    throw new BusinessRuleException("Kaynak ve hedef hesap aynı para birimini kullanmalıdır.");
                await EnsureAvailableBalanceAsync(account.Id, request.Amount, cancellationToken);
            }
        }

        var risk = PaymentPolicy.RiskFor(request.Amount);
        var now = DateTime.UtcNow;
        var correlationId = Guid.NewGuid();

        var approval = new PaymentApproval
        {
            RequestType = requestType,
            FromAccountId = account?.Id ?? Guid.Empty,
            ToAccountId = requestType == ApprovalRequestType.AccountOpening
                ? Guid.Empty
                : toAccount?.Id ?? account!.Id,
            RequestedCustomerId = customerIdForOpening,
            RequestedInitialBalance = requestType == ApprovalRequestType.AccountOpening ? request.Amount : null,
            RequestedAccountName = requestedAccountName,
            RequestedAccountType = requestedAccountType,
            Amount = request.Amount,
            Currency = currency,
            Description = description,
            RequestedByUserId = dbActor.UserId,
            CorrelationId = correlationId,
            IsDemo = isDemo,
            RiskLevel = risk,
            Status = PaymentApprovalStatus.Pending,
            ApprovalReason = PaymentPolicy.DescribeRisk(risk),
            CreatedAt = now
        };

        // The approval row, its timeline and the audit entry must be created
        // atomically. The generated identity is only available after the first
        // save, so both saves share one transaction.
        await using var createTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _context.PaymentApprovals.Add(approval);
            await _context.SaveChangesAsync(cancellationToken);

            AddTimeline(approval, PaymentTimelineEventType.Created, now, dbActor, false,
                $"{RequestTypeLabel(requestType)} talebi oluşturuldu.");
            AddTimeline(approval, PaymentTimelineEventType.Pending, now.AddTicks(1), dbActor, true,
                "Owner veya yetkili onayı bekleniyor.");

            _auditService.Add("APPROVAL_REQUEST_CREATED", "PaymentApproval", approval.Id.ToString(), dbActor.Username, true,
                $"Type: {requestType}, Amount: {approval.Amount}, Currency: {approval.Currency}, " +
                $"Requester: {dbActor.UserId}, Correlation: {approval.CorrelationId}");

            await _context.SaveChangesAsync(cancellationToken);
            await createTransaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await SafeRollbackAsync(createTransaction, cancellationToken);
            throw;
        }

        return _mapper.Map<PaymentApprovalResponse>(approval);
    }

    public async Task<List<PaymentApprovalResponse>> GetPendingApprovalsAsync(ActorContext actor, CancellationToken cancellationToken = default)
    {
        return await GetQueueAsync(actor, "Pending", null, null, null, cancellationToken);
    }

    public async Task<List<PaymentApprovalResponse>> GetQueueAsync(
        ActorContext actor,
        string? status,
        string? requestType,
        string? currency,
        string? scope,
        CancellationToken cancellationToken = default)
    {
        var dbActor = await _authorization.RequireActiveActorAsync(actor.UserId, cancellationToken);
        var query = _context.PaymentApprovals.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<PaymentApprovalStatus>(status, ignoreCase: true, out var parsedStatus))
                throw new BusinessRuleException("Geçersiz talep durumu.");
            query = query.Where(x => x.Status == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(requestType))
        {
            var parsedType = ParseRequestType(requestType);
            query = query.Where(x => x.RequestType == parsedType);
        }

        if (!string.IsNullOrWhiteSpace(currency))
        {
            var normalized = CurrencyCodes.Normalize(currency);
            query = query.Where(x => x.Currency == normalized);
        }

        List<PaymentApproval> items;
        if (dbActor.Role == UserRole.Customer)
        {
            items = await query.Where(x => x.RequestedByUserId == dbActor.UserId)
                .OrderByDescending(x => x.CreatedAt).Take(500).ToListAsync(cancellationToken);
        }
        else if (dbActor.Role == UserRole.DelegatedApprover)
        {
            if (string.Equals(scope, "reviewed", StringComparison.OrdinalIgnoreCase))
            {
                items = await query.Where(x => x.ReviewedByUserId == dbActor.UserId)
                    .OrderByDescending(x => x.ReviewedAt).Take(500).ToListAsync(cancellationToken);
            }
            else
            {
                var candidate = await query.Where(x => x.RequestedByUserId != dbActor.UserId)
                    .OrderByDescending(x => x.CreatedAt).Take(500).ToListAsync(cancellationToken);
                items = new List<PaymentApproval>();
                foreach (var item in candidate)
                {
                    var permission = await _authorization.CheckAsync(
                        dbActor.UserId, OperationFor(item.RequestType), item.Currency, item.Amount, cancellationToken);
                    if (permission.Allowed) items.Add(item);
                }
            }
        }
        else
        {
            items = await query.OrderByDescending(x => x.CreatedAt).Take(500).ToListAsync(cancellationToken);
        }

        return items.Select(_mapper.Map<PaymentApprovalResponse>).ToList();
    }

    public async Task<PaymentApprovalResponse> GetByIdAsync(int approvalId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var dbActor = await _authorization.RequireActiveActorAsync(actor.UserId, cancellationToken);
        var approval = await _context.PaymentApprovals.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == approvalId, cancellationToken)
            ?? throw new NotFoundException("Payment", approvalId.ToString());

        if (dbActor.Role == UserRole.Customer)
        {
            if (!await _customerAccess.UserOwnsPaymentAsync(dbActor.UserId, approvalId, cancellationToken))
                throw new NotFoundException("Payment", approvalId.ToString());
        }
        else if (dbActor.Role == UserRole.DelegatedApprover)
        {
            var permission = await _authorization.CheckAsync(
                dbActor.UserId, OperationFor(approval.RequestType), approval.Currency, approval.Amount, cancellationToken);
            if (!permission.Allowed)
                throw new NotFoundException("Payment", approvalId.ToString());
        }

        return _mapper.Map<PaymentApprovalResponse>(approval);
    }

    public async Task ApprovePaymentAsync(int approvalId, ActorContext reviewer, CancellationToken cancellationToken = default)
    {
        var dbReviewer = await _authorization.RequireActiveActorAsync(reviewer.UserId, cancellationToken);
        if (dbReviewer.Role is not (UserRole.Owner or UserRole.DelegatedApprover))
            throw new ForbiddenException("Onay yalnız Owner veya DelegatedApprover tarafından yapılabilir.");

        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var approval = await _context.PaymentApprovals
                .FirstOrDefaultAsync(x => x.Id == approvalId, cancellationToken)
                ?? throw new NotFoundException("Payment", approvalId.ToString());

            if (approval.Status != PaymentApprovalStatus.Pending)
                throw new BusinessRuleException("Yalnız bekleyen talepler onaylanabilir.");
            if (approval.RequestedByUserId == dbReviewer.UserId)
                throw new BusinessRuleException("Kendi oluşturduğunuz talebi onaylayamazsınız (maker-checker).");

            await EnsureRequestOwnershipStillValidAsync(approval, cancellationToken);

            var permission = await _authorization.CheckAsync(
                dbReviewer.UserId, OperationFor(approval.RequestType), approval.Currency, approval.Amount, cancellationToken);
            if (!permission.Allowed)
                throw new BusinessRuleException(permission.Reason);

            var now = DateTime.UtcNow;
            Guid? relatedTransactionId = null;
            Guid? createdAccountId = null;

            switch (approval.RequestType)
            {
                case ApprovalRequestType.Transfer:
                {
                    var from = await RequireAccountAsync(approval.FromAccountId, cancellationToken);
                    var to = await RequireAccountAsync(approval.ToAccountId, cancellationToken);
                    if (!string.Equals(from.Currency, approval.Currency, StringComparison.OrdinalIgnoreCase)
                        || !string.Equals(to.Currency, approval.Currency, StringComparison.OrdinalIgnoreCase))
                        throw new BusinessRuleException("Para birimi hesaplarla uyuşmuyor.");
                    var (debit, credit) = LedgerOperations.ApplyTransfer(
                        _context, from, to, approval.Amount, approval.Description, now, approval.CorrelationId, approval.IsDemo);
                    JournalPoster.AddTransfer(_context, approval, from, to, debit.Id, credit.Id, now);
                    relatedTransactionId = debit.Id;
                    break;
                }
                case ApprovalRequestType.Deposit:
                {
                    var account = await RequireAccountAsync(approval.FromAccountId, cancellationToken);
                    if (!string.Equals(account.Currency, approval.Currency, StringComparison.OrdinalIgnoreCase))
                        throw new BusinessRuleException("Para birimi hesapla uyuşmuyor.");
                    var tx = LedgerOperations.ApplyDeposit(
                        _context, account, approval.Amount, approval.Description, now,
                        "Onaylanan demo yatırma", approval.CorrelationId, approval.IsDemo);
                    JournalPoster.AddDeposit(_context, approval, account, tx.Id, now);
                    relatedTransactionId = tx.Id;
                    break;
                }
                case ApprovalRequestType.Withdrawal:
                {
                    var account = await RequireAccountAsync(approval.FromAccountId, cancellationToken);
                    if (!string.Equals(account.Currency, approval.Currency, StringComparison.OrdinalIgnoreCase))
                        throw new BusinessRuleException("Para birimi hesapla uyuşmuyor.");
                    var tx = LedgerOperations.ApplyWithdraw(
                        _context, account, approval.Amount, approval.Description, now,
                        "Onaylanan demo çekme", approval.CorrelationId, approval.IsDemo);
                    JournalPoster.AddWithdrawal(_context, approval, account, tx.Id, now);
                    relatedTransactionId = tx.Id;
                    break;
                }
                case ApprovalRequestType.AccountOpening:
                {
                    var customer = await _context.Customers
                        .FirstOrDefaultAsync(x => x.Id == approval.RequestedCustomerId, cancellationToken)
                        ?? throw new NotFoundException("Customer", approval.RequestedCustomerId?.ToString() ?? "");
                    var account = new Account
                    {
                        Id = Guid.NewGuid(),
                        AccountNumber = await GenerateUniqueAccountNumberAsync(cancellationToken),
                        CustomerId = customer.Id,
                        AccountName = approval.RequestedAccountName ?? AccountMetadata.DefaultName(approval.RequestedAccountType ?? AccountType.Checking),
                        AccountType = approval.RequestedAccountType ?? AccountType.Checking,
                        Currency = approval.Currency,
                        Balance = approval.RequestedInitialBalance ?? approval.Amount,
                        IsDemo = approval.IsDemo,
                        CreatedDate = now
                    };
                    _context.Accounts.Add(account);
                    createdAccountId = account.Id;
                    if (account.Balance > 0)
                    {
                        var opening = new Transaction
                        {
                            Id = Guid.NewGuid(),
                            AccountId = account.Id,
                            Type = TransactionType.Deposit,
                            Amount = account.Balance,
                            BalanceAfter = account.Balance,
                            Description = "Onaylanan hesap açılışı",
                            CorrelationId = approval.CorrelationId,
                            IsDemo = approval.IsDemo,
                            CreatedDate = now
                        };
                        _context.Transactions.Add(opening);
                        relatedTransactionId = opening.Id;
                        JournalPoster.AddAccountOpening(_context, approval, account, opening.Id, now);
                    }
                    break;
                }
            }

            approval.Status = PaymentApprovalStatus.Approved;
            approval.ReviewedByUserId = dbReviewer.UserId;
            approval.ReviewedAt = now;
            approval.RejectionReason = null;
            approval.RelatedTransactionId = relatedTransactionId;
            if (createdAccountId is not null) approval.CreatedAccountId = createdAccountId;

            AddTimeline(approval, PaymentTimelineEventType.Approved, now, dbReviewer, false, "Yetkili tarafından onaylandı.");
            AddTimeline(approval, PaymentTimelineEventType.Completed, now.AddTicks(1), dbReviewer, true, "Finansal talep uygulandı.");
            _auditService.Add("APPROVAL_REQUEST_APPROVED", "PaymentApproval", approval.Id.ToString(), dbReviewer.Username, true,
                $"Type: {approval.RequestType}, Amount: {approval.Amount}, Currency: {approval.Currency}, " +
                $"Reviewer: {dbReviewer.UserId}, Delegation: {permission.DelegationId?.ToString() ?? "owner"}");

            await _context.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await SafeRollbackAsync(dbTransaction, cancellationToken);
            throw new ConcurrencyException("Talep başka bir incelemeci tarafından işlendi.");
        }
        catch
        {
            await SafeRollbackAsync(dbTransaction, cancellationToken);
            throw;
        }
    }

    public async Task RejectPaymentAsync(int approvalId, ActorContext reviewer, string rejectionReason, CancellationToken cancellationToken = default)
    {
        var dbReviewer = await _authorization.RequireActiveActorAsync(reviewer.UserId, cancellationToken);
        if (dbReviewer.Role is not (UserRole.Owner or UserRole.DelegatedApprover))
            throw new ForbiddenException("Red yalnız Owner veya DelegatedApprover tarafından yapılabilir.");

        var reason = (rejectionReason ?? string.Empty).Trim();
        if (reason.Length < 3 || reason.Length > 500)
            throw new BusinessRuleException("Red gerekçesi 3–500 karakter olmalıdır.");

        var approval = await _context.PaymentApprovals.FirstOrDefaultAsync(x => x.Id == approvalId, cancellationToken)
                       ?? throw new NotFoundException("Payment", approvalId.ToString());
        if (approval.Status != PaymentApprovalStatus.Pending)
            throw new BusinessRuleException("Yalnız bekleyen talepler reddedilebilir.");
        if (approval.RequestedByUserId == dbReviewer.UserId)
            throw new BusinessRuleException("Kendi oluşturduğunuz talebi reddedemezsiniz.");

        var permission = await _authorization.CheckAsync(
            dbReviewer.UserId, OperationFor(approval.RequestType), approval.Currency, approval.Amount, cancellationToken);
        if (!permission.Allowed)
            throw new BusinessRuleException(permission.Reason);

        var now = DateTime.UtcNow;
        approval.Status = PaymentApprovalStatus.Rejected;
        approval.ReviewedByUserId = dbReviewer.UserId;
        approval.ReviewedAt = now;
        approval.RejectionReason = reason;
        AddTimeline(approval, PaymentTimelineEventType.Rejected, now, dbReviewer, false, "Yetkili tarafından reddedildi.");
        _auditService.Add("APPROVAL_REQUEST_REJECTED", "PaymentApproval", approval.Id.ToString(), dbReviewer.Username, true,
            $"Reason: {reason}");
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            throw new ConcurrencyException("Talep başka bir incelemeci tarafından işlendi.");
        }
    }

    public async Task CancelAsync(int approvalId, ActorContext actor, CancellationToken cancellationToken = default)
    {
        var dbActor = await _authorization.RequireActiveActorAsync(actor.UserId, cancellationToken);
        var approval = await _context.PaymentApprovals.FirstOrDefaultAsync(x => x.Id == approvalId, cancellationToken)
                       ?? throw new NotFoundException("Payment", approvalId.ToString());

        if (approval.Status != PaymentApprovalStatus.Pending)
            throw new BusinessRuleException("Yalnız bekleyen talep iptal edilebilir.");
        if (dbActor.Role != UserRole.Owner && approval.RequestedByUserId != dbActor.UserId)
            throw new ForbiddenException("Bu talebi iptal edemezsiniz.");

        var now = DateTime.UtcNow;
        approval.Status = PaymentApprovalStatus.Cancelled;
        approval.ReviewedAt = now;
        AddTimeline(approval, PaymentTimelineEventType.Cancelled, now, dbActor, false, "Talep iptal edildi.");
        _auditService.Add("APPROVAL_REQUEST_CANCELLED", "PaymentApproval", approval.Id.ToString(), dbActor.Username, true,
            "Pending request cancelled; no balance changed.");
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _context.ChangeTracker.Clear();
            throw new ConcurrencyException("Talep başka bir kullanıcı tarafından değiştirildi.");
        }
    }

    private async Task EnsureRequestOwnershipStillValidAsync(PaymentApproval approval, CancellationToken cancellationToken)
    {
        if (approval.RequestedCustomerId is not { } requestedCustomerId
            || approval.RequestType == ApprovalRequestType.AccountOpening)
            return;

        var customer = await _context.Customers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == requestedCustomerId, cancellationToken);
        if (customer?.UserId != approval.RequestedByUserId)
            throw new BusinessRuleException(
                "Talebi oluşturan kullanıcı artık bu müşteri profiline bağlı değil; onay güvenlik nedeniyle reddedildi.");

        var accountCustomerId = await _context.Accounts.AsNoTracking()
            .Where(x => x.Id == approval.FromAccountId)
            .Select(x => x.CustomerId)
            .FirstOrDefaultAsync(cancellationToken);
        if (accountCustomerId != requestedCustomerId)
            throw new BusinessRuleException(
                "Hesap sahipliği talep oluşturulduktan sonra değişmiş; onay güvenlik nedeniyle reddedildi.");
    }

    private async Task EnsureAvailableBalanceAsync(Guid accountId, decimal amount, CancellationToken cancellationToken)
    {
        var account = await RequireAccountAsync(accountId, cancellationToken);
        var pendingOutgoing = await _context.PaymentApprovals.AsNoTracking()
            .Where(x => x.Status == PaymentApprovalStatus.Pending
                        && x.FromAccountId == accountId
                        && (x.RequestType == ApprovalRequestType.Transfer || x.RequestType == ApprovalRequestType.Withdrawal))
            .Select(x => x.Amount)
            .ToListAsync(cancellationToken);

        var available = account.Balance - pendingOutgoing.Sum();
        if (amount > available)
            throw new BusinessRuleException(
                $"Yetersiz kullanılabilir bakiye. Bakiye: {account.Balance:N2}, bekleyen çıkış: {pendingOutgoing.Sum():N2}, kullanılabilir: {available:N2}.");
    }

    private async Task<Account> RequireAccountAsync(Guid accountId, CancellationToken cancellationToken) =>
        await _context.Accounts.FirstOrDefaultAsync(x => x.Id == accountId, cancellationToken)
        ?? throw new NotFoundException("Account", accountId.ToString());

    private void AddTimeline(
        PaymentApproval approval,
        PaymentTimelineEventType eventType,
        DateTime occurredAtUtc,
        ActorContext actor,
        bool isSystem,
        string description)
    {
        _context.PaymentTimelineEvents.Add(new PaymentTimelineEvent
        {
            Id = Guid.NewGuid(),
            PaymentApprovalId = approval.Id,
            CorrelationId = approval.CorrelationId,
            EventType = eventType,
            OccurredAtUtc = occurredAtUtc,
            ActorUserId = isSystem ? null : actor.UserId,
            ActorUsername = isSystem ? "system" : actor.Username,
            ActorRole = isSystem ? "System" : actor.Role.ToString(),
            Description = description,
            IsSystem = isSystem
        });
    }

    private async Task<string> GenerateUniqueAccountNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = $"ACC{DateTime.UtcNow:yyyyMMddHHmmss}{RandomNumberGenerator.GetInt32(100000, 999999)}";
            if (!await _context.Accounts.AnyAsync(x => x.AccountNumber == candidate, cancellationToken))
                return candidate;
        }
        throw new BusinessRuleException("Benzersiz hesap numarası üretilemedi.");
    }

    private static ApprovalRequestType ParseRequestType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !Enum.TryParse<ApprovalRequestType>(value.Trim(), ignoreCase: true, out var parsed))
            throw new BusinessRuleException("Geçersiz talep türü.");
        return parsed;
    }

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

    private static async Task SafeRollbackAsync(Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        try { await transaction.RollbackAsync(cancellationToken); }
        catch (InvalidOperationException) { }
    }
}
