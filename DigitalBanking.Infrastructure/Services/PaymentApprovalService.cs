using AutoMapper;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace DigitalBanking.Infrastructure.Services;

/// <summary>
/// Maker-checker payment approval workflow.
///
/// Policy (see docs/adr/0001-payment-and-role-policy.md):
/// - low risk (&lt; 50,000 in the account currency): auto-approved by the system;
/// - medium/high/critical: Pending until a different Admin approves or rejects;
/// - requester and reviewer must differ (maker-checker);
/// - both accounts must exist, share a currency and have sufficient funds;
/// - ledger rows, approval state, timeline and audit record are committed atomically;
/// - a rowversion token prevents two concurrent approvals from both applying money.
/// </summary>
public class PaymentApprovalService : IPaymentApprovalService
{
    private readonly BankDbContext _context;
    private readonly IMapper _mapper;
    private readonly IAuditService _auditService;
    private readonly ICustomerAccessService _customerAccess;
    private readonly ILogger _logger;

    public PaymentApprovalService(
        BankDbContext context,
        IMapper mapper,
        IAuditService auditService,
        ICustomerAccessService customerAccess)
    {
        _context = context;
        _mapper = mapper;
        _auditService = auditService;
        _customerAccess = customerAccess;
        _logger = Log.ForContext<PaymentApprovalService>();
    }

    public Task<PaymentApprovalResponse> CreateApprovalRequestAsync(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string? description,
        ActorContext actor,
        CancellationToken cancellationToken = default) =>
        CreateCoreAsync(fromAccountId, toAccountId, amount, description, actor, requireAdmin: true, isDemo: false, cancellationToken);

    public Task<PaymentApprovalResponse> CreateCustomerTransferRequestAsync(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string? description,
        ActorContext actor,
        CancellationToken cancellationToken = default) =>
        CreateCoreAsync(fromAccountId, toAccountId, amount, description, actor, requireAdmin: false, isDemo: false, cancellationToken);

    private async Task<PaymentApprovalResponse> CreateCoreAsync(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string? description,
        ActorContext actor,
        bool requireAdmin,
        bool isDemo,
        CancellationToken cancellationToken)
    {
        if (requireAdmin)
            EnsureAdmin(actor);
        else if (!await _customerAccess.UserOwnsAccountAsync(actor.UserId, fromAccountId, cancellationToken))
            throw new NotFoundException("Account", fromAccountId);

        Money.EnsurePositive(amount);

        if (fromAccountId == toAccountId)
            throw new BusinessRuleException("Sender and receiver accounts cannot be the same.");

        if (description is { Length: > 250 })
            throw new BusinessRuleException("Description cannot exceed 250 characters.");

        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var fromAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == fromAccountId, cancellationToken);
            if (fromAccount is null)
                throw new NotFoundException("Account", fromAccountId);

            var toAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == toAccountId, cancellationToken);
            if (toAccount is null)
                throw new NotFoundException("Account", toAccountId);

            if (!string.Equals(fromAccount.Currency, toAccount.Currency, StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("Sender and receiver accounts must use the same currency.");

            if (fromAccount.Balance < amount)
                throw new BusinessRuleException("Insufficient balance in source account.");

            var riskLevel = PaymentPolicy.RiskFor(amount);
            var requiresChecker = PaymentPolicy.RequiresMakerChecker(amount);
            var now = DateTime.UtcNow;
            var correlationId = Guid.NewGuid();

            var approval = new PaymentApproval
            {
                FromAccountId = fromAccountId,
                ToAccountId = toAccountId,
                Amount = amount,
                Currency = fromAccount.Currency,
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                RequestedByUserId = actor.UserId,
                CorrelationId = correlationId,
                IsDemo = isDemo,
                RiskLevel = riskLevel,
                Status = requiresChecker ? PaymentApprovalStatus.Pending : PaymentApprovalStatus.AutoApproved,
                ApprovalReason = PaymentPolicy.DescribeRisk(riskLevel),
                CreatedAt = now,
                ReviewedAt = requiresChecker ? null : now
            };

            _context.PaymentApprovals.Add(approval);

            Transaction? debit = null;
            Transaction? credit = null;
            if (!requiresChecker)
            {
                (debit, credit) = LedgerOperations.ApplyTransfer(
                    _context, fromAccount, toAccount, amount, approval.Description, now, correlationId, isDemo);
                approval.RelatedTransactionId = debit.Id;
            }

            // First save assigns the identity id before timeline rows reference it.
            await _context.SaveChangesAsync(cancellationToken);

            AddTimeline(approval, PaymentTimelineEventType.Created, now, actor, isSystem: false,
                "Payment request created.");
            if (requiresChecker)
            {
                AddTimeline(approval, PaymentTimelineEventType.Pending, now.AddTicks(1), actor, isSystem: true,
                    "Awaiting a different Admin (maker-checker).");
            }
            else
            {
                AddTimeline(approval, PaymentTimelineEventType.AutoApproved, now.AddTicks(1), actor, isSystem: true,
                    "Low risk policy auto-approved the request.");
                AddTimeline(approval, PaymentTimelineEventType.Completed, now.AddTicks(2), actor, isSystem: true,
                    "Ledger movement completed.");
            }

            _auditService.Add(
                action: requiresChecker ? "PAYMENT_CREATED" : "PAYMENT_AUTO_APPROVED",
                entityName: "PaymentApproval",
                entityId: approval.Id.ToString(),
                username: actor.Username,
                isSuccess: true,
                details:
                    $"Payment request {(requiresChecker ? "created" : "auto-approved")}. " +
                    $"FromAccountId: {approval.FromAccountId}, ToAccountId: {approval.ToAccountId}, " +
                    $"Amount: {approval.Amount}, Currency: {approval.Currency}, RiskLevel: {approval.RiskLevel}, " +
                    $"CorrelationId: {approval.CorrelationId}, RequestedByUserId: {approval.RequestedByUserId}" +
                    (debit is null ? string.Empty : $", DebitTransactionId: {debit.Id}, CreditTransactionId: {credit!.Id}"));

            await _context.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            _logger.Information(
                "Payment approval {ApprovalId} {Status}. Amount: {Amount}, Risk: {Risk}, Requester: {Requester}",
                approval.Id, approval.Status, approval.Amount, approval.RiskLevel, approval.RequestedByUserId);

            return _mapper.Map<PaymentApprovalResponse>(approval);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyException("PaymentApproval", fromAccountId);
        }
        catch
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<List<PaymentApprovalResponse>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default)
    {
        var approvals = await _context.PaymentApprovals
            .AsNoTracking()
            .Where(x => x.Status == PaymentApprovalStatus.Pending)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return _mapper.Map<List<PaymentApprovalResponse>>(approvals);
    }

    public async Task<PaymentApprovalResponse> GetByIdAsync(int approvalId, CancellationToken cancellationToken = default)
    {
        var approval = await _context.PaymentApprovals
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == approvalId, cancellationToken);

        if (approval is null)
            throw new NotFoundException($"Payment approval request with id {approvalId} was not found.");

        return _mapper.Map<PaymentApprovalResponse>(approval);
    }

    public async Task ApprovePaymentAsync(int approvalId, ActorContext reviewer, CancellationToken cancellationToken = default)
    {
        EnsureAdmin(reviewer);

        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var approval = await _context.PaymentApprovals
                .FirstOrDefaultAsync(x => x.Id == approvalId, cancellationToken);

            if (approval is null)
                throw new NotFoundException($"Payment approval request with id {approvalId} was not found.");

            if (approval.Status != PaymentApprovalStatus.Pending)
                throw new BusinessRuleException("Only pending payment approvals can be approved.");

            if (approval.RequestedByUserId == reviewer.UserId)
                throw new BusinessRuleException("Users cannot review their own payment requests.");

            var fromAccount = await _context.Accounts
                .FirstOrDefaultAsync(x => x.Id == approval.FromAccountId, cancellationToken);
            if (fromAccount is null)
                throw new NotFoundException($"Sender account with id {approval.FromAccountId} was not found.");

            var toAccount = await _context.Accounts
                .FirstOrDefaultAsync(x => x.Id == approval.ToAccountId, cancellationToken);
            if (toAccount is null)
                throw new NotFoundException($"Receiver account with id {approval.ToAccountId} was not found.");

            if (!string.Equals(fromAccount.Currency, approval.Currency, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(toAccount.Currency, approval.Currency, StringComparison.OrdinalIgnoreCase))
                throw new BusinessRuleException("Payment currency no longer matches the account currencies.");

            var now = DateTime.UtcNow;
            var (debit, credit) = LedgerOperations.ApplyTransfer(
                _context, fromAccount, toAccount, approval.Amount, approval.Description, now,
                approval.CorrelationId, approval.IsDemo);

            approval.Status = PaymentApprovalStatus.Approved;
            approval.ReviewedByUserId = reviewer.UserId;
            approval.ReviewedAt = now;
            approval.RejectionReason = null;
            approval.RelatedTransactionId = debit.Id;

            AddTimeline(approval, PaymentTimelineEventType.Approved, now, reviewer, isSystem: false,
                "Payment approved by checker.");
            AddTimeline(approval, PaymentTimelineEventType.Completed, now.AddTicks(1), reviewer, isSystem: true,
                "Ledger movement completed.");

            _auditService.Add(
                action: "PAYMENT_APPROVED",
                entityName: "PaymentApproval",
                entityId: approval.Id.ToString(),
                username: reviewer.Username,
                isSuccess: true,
                details:
                    $"Payment approved. FromAccountId: {approval.FromAccountId}, ToAccountId: {approval.ToAccountId}, " +
                    $"Amount: {approval.Amount}, Currency: {approval.Currency}, " +
                    $"CorrelationId: {approval.CorrelationId}, ReviewedByUserId: {reviewer.UserId}, " +
                    $"DebitTransactionId: {debit.Id}, CreditTransactionId: {credit.Id}");

            await _context.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            _logger.Information(
                "Payment approval {ApprovalId} approved by {ReviewerId}. Amount: {Amount}",
                approval.Id, reviewer.UserId, approval.Amount);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyException(
                $"Payment approval {approvalId} was processed by another reviewer. Please refresh.");
        }
        catch
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RejectPaymentAsync(int approvalId, ActorContext reviewer, string rejectionReason, CancellationToken cancellationToken = default)
    {
        EnsureAdmin(reviewer);

        if (string.IsNullOrWhiteSpace(rejectionReason))
            throw new BusinessRuleException("A rejection reason is required.");

        if (rejectionReason.Length > 500)
            throw new BusinessRuleException("Rejection reason cannot exceed 500 characters.");

        await using var dbTransaction = await _context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var approval = await _context.PaymentApprovals
                .FirstOrDefaultAsync(x => x.Id == approvalId, cancellationToken);

            if (approval is null)
                throw new NotFoundException($"Payment approval request with id {approvalId} was not found.");

            if (approval.Status != PaymentApprovalStatus.Pending)
                throw new BusinessRuleException("Only pending payment approvals can be rejected.");

            if (approval.RequestedByUserId == reviewer.UserId)
                throw new BusinessRuleException("Users cannot review their own payment requests.");

            var now = DateTime.UtcNow;
            approval.Status = PaymentApprovalStatus.Rejected;
            approval.ReviewedByUserId = reviewer.UserId;
            approval.ReviewedAt = now;
            approval.RejectionReason = rejectionReason.Trim();

            AddTimeline(approval, PaymentTimelineEventType.Rejected, now, reviewer, isSystem: false,
                "Payment rejected by checker.");

            _auditService.Add(
                action: "PAYMENT_REJECTED",
                entityName: "PaymentApproval",
                entityId: approval.Id.ToString(),
                username: reviewer.Username,
                isSuccess: true,
                details:
                    $"Payment rejected. FromAccountId: {approval.FromAccountId}, ToAccountId: {approval.ToAccountId}, " +
                    $"Amount: {approval.Amount}, Currency: {approval.Currency}, " +
                    $"CorrelationId: {approval.CorrelationId}, ReviewedByUserId: {reviewer.UserId}, " +
                    $"RejectionReason: {approval.RejectionReason}");

            await _context.SaveChangesAsync(cancellationToken);
            await dbTransaction.CommitAsync(cancellationToken);

            _logger.Information(
                "Payment approval {ApprovalId} rejected by {ReviewerId}.",
                approval.Id, reviewer.UserId);
        }
        catch (DbUpdateConcurrencyException)
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw new ConcurrencyException(
                $"Payment approval {approvalId} was processed by another reviewer. Please refresh.");
        }
        catch
        {
            await dbTransaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

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

    private static void EnsureAdmin(ActorContext actor)
    {
        if (!actor.IsAdmin)
            throw new BusinessRuleException("Payment approval operations are restricted to the Admin role.");
    }
}
