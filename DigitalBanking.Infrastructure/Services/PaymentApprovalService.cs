using AutoMapper;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;


namespace DigitalBanking.Infrastructure.Services
{
    public class PaymentApprovalService : IPaymentApprovalService
    {
        private readonly BankDbContext _context;
        private readonly IMapper _mapper;
        private readonly IAuditService _auditService;

        public PaymentApprovalService(
            BankDbContext context,
            IMapper mapper,
            IAuditService auditService)
        {
            _context = context;
            _mapper = mapper;
            _auditService = auditService;
        }

        public async Task<PaymentApprovalResponse> CreateApprovalRequestAsync(
            Guid fromAccountId,
            Guid toAccountId,
            decimal amount,
            string description,
            int requestedByUserId)
        {
            var riskLevel = CalculateRiskLevel(amount);

            var approval = new PaymentApproval
            {
                FromAccountId = fromAccountId,
                ToAccountId = toAccountId,
                Amount = amount,
                Currency = "TRY", // Assuming the currency is Turkish Lira for this example
                Description = description,
                RequestedByUserId = requestedByUserId,
                RiskLevel = riskLevel,
                Status = PaymentApprovalStatus.Pending,
                ApprovalReason = GetApprovalReason(riskLevel, amount),
                CreatedAt = DateTime.UtcNow
            };
            _context.PaymentApprovals.Add(approval);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                action: "PAYMENT_CREATED",
                entityName: "PaymentApproval",
                entityId: approval.Id.ToString(),
                username: null,
                isSuccess: true,
                details:
                    $"Payment approval request created. " +
                    $"FromAccountId: {approval.FromAccountId}, " +
                    $"ToAccountId: {approval.ToAccountId}, " +
                    $"Amount: {approval.Amount}, " +
                    $"Currency: {approval.Currency}, " +
                    $"RiskLevel: {approval.RiskLevel}, " +
                    $"RequestedByUserId: {approval.RequestedByUserId}");

            return _mapper.Map<PaymentApprovalResponse>(approval);
        }
        public async Task<List<PaymentApprovalResponse>> GetPendingApprovalsAsync()
        {
            var approvals = await _context.PaymentApprovals
                .Where(x => x.Status == PaymentApprovalStatus.Pending)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return _mapper.Map<List<PaymentApprovalResponse>>(approvals);
        }
        public async Task ApprovePaymentAsync(int approvalId, int reviewedByUserId)
        {
            var approval = await _context.PaymentApprovals
                .FirstOrDefaultAsync(x => x.Id == approvalId);

            if (approval is null)
                throw new NotFoundException($"Payment approval request with id {approvalId} was not found.");

            if (approval.Status != PaymentApprovalStatus.Pending)
                throw new BusinessRuleException("Only pending payment approvals can be approved.");

            if (approval.RequestedByUserId == reviewedByUserId)
                throw new BusinessRuleException("Users cannot approve their own payment requests.");

            var fromAccount = await _context.Accounts
                .FirstOrDefaultAsync(x => x.Id == approval.FromAccountId);

            if (fromAccount is null)
                throw new NotFoundException($"Sender account with id {approval.FromAccountId} was not found.");

            if (fromAccount.Balance < approval.Amount)
                throw new BusinessRuleException("Insufficient balance.");

            var toAccount = await _context.Accounts
                .FirstOrDefaultAsync(x => x.Id == approval.ToAccountId);

            if (toAccount is null)
                throw new NotFoundException($"Receiver account with id {approval.ToAccountId} was not found.");

            if (fromAccount.Balance < approval.Amount)
                throw new InvalidOperationException("Insufficient balance.");

            // Transfer operation
            fromAccount.Balance -= approval.Amount;
            toAccount.Balance += approval.Amount;

            // Sender transaction
            var debitTransaction = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = fromAccount.Id,
                Type = TransactionType.TransferOut,
                Amount = approval.Amount,
                BalanceAfter = fromAccount.Balance,
                Description = approval.Description,
                CreatedDate = DateTime.UtcNow
            };

            // Receiver transaction
            var creditTransaction = new Transaction
            {
                Id = Guid.NewGuid(),
                AccountId = toAccount.Id,
                Type = TransactionType.TransferIn,
                Amount = approval.Amount,
                BalanceAfter = toAccount.Balance,
                Description = approval.Description,
                CreatedDate = DateTime.UtcNow
            };

            _context.Transactions.Add(debitTransaction);
            _context.Transactions.Add(creditTransaction);

            approval.Status = PaymentApprovalStatus.Approved;
            approval.ReviewedByUserId = reviewedByUserId;
            approval.ReviewedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await _auditService.LogAsync(
                action: "PAYMENT_APPROVED",
                entityName: "PaymentApproval",
                entityId: approval.Id.ToString(),
                username: null,
                isSuccess: true,
                details:
                $"Payment approved. " +
                $"FromAccountId: {approval.FromAccountId}, " +
                $"ToAccountId: {approval.ToAccountId}, " +
                $"Amount: {approval.Amount}, " +
                $"ReviewedByUserId: {reviewedByUserId}, " +
                $"DebitTransactionId: {debitTransaction.Id}, " +
                $"CreditTransactionId: {creditTransaction.Id}");
            }

        public async Task RejectPaymentAsync(
            int approvalId,
            int reviewedByUserId,
            string rejectionReason)
        {
            var approval = await _context.PaymentApprovals
                .FirstOrDefaultAsync(x => x.Id == approvalId);

            if (approval is null)
                throw new NotFoundException($"Payment approval request with id {approvalId} was not found.");

            if (approval.Status != PaymentApprovalStatus.Pending)
                throw new BusinessRuleException("Only pending payment approvals can be rejected.");

            approval.Status = PaymentApprovalStatus.Rejected;
            approval.ReviewedByUserId = reviewedByUserId;
            approval.ReviewedAt = DateTime.UtcNow;
            approval.RejectionReason = rejectionReason;

            await _context.SaveChangesAsync();
            await _auditService.LogAsync(
    action: "PAYMENT_REJECTED",
    entityName: "PaymentApproval",
    entityId: approval.Id.ToString(),
    username: null,
    isSuccess: true,
    details:
        $"Payment rejected. " +
        $"FromAccountId: {approval.FromAccountId}, " +
        $"ToAccountId: {approval.ToAccountId}, " +
        $"Amount: {approval.Amount}, " +
        $"ReviewedByUserId: {reviewedByUserId}, " +
        $"RejectionReason: {rejectionReason}");
        }

        private PaymentRiskLevel CalculateRiskLevel(decimal amount)
        {
            if (amount >= 1_000_000)
                return PaymentRiskLevel.Critical;

            if (amount >= 250_000)
                return PaymentRiskLevel.High;

            if (amount >= 50_000)
                return PaymentRiskLevel.Medium;

            return PaymentRiskLevel.Low;
        }

        private string GetApprovalReason(PaymentRiskLevel riskLevel, decimal amount)
        {
            return riskLevel switch
            {
                PaymentRiskLevel.Critical => $"Critical risk payment. Amount {amount:N2} requires senior approval.",
                PaymentRiskLevel.High => $"High value payment. Amount {amount:N2} requires operational approval.",
                PaymentRiskLevel.Medium => $"Medium risk payment. Amount {amount:N2} requires review.",
                _ => "Low risk payment approval request created."
            };
        }
        public async Task<PaymentApprovalResponse> GetByIdAsync(int approvalId)
        {
            var approval = await _context.PaymentApprovals
                .FirstOrDefaultAsync(x => x.Id == approvalId);

            if (approval is null)
                throw new NotFoundException($"Payment approval request with id {approvalId} was not found.");

            return _mapper.Map<PaymentApprovalResponse>(approval);
        }
    }
}
