using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;


namespace DigitalBanking.Infrastructure.Services
{
    public class PaymentApprovalService : IPaymentApprovalService
    {
        private readonly BankDbContext _context;

        public PaymentApprovalService(BankDbContext context)
        {
            _context = context;
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
            return MapToResponse(approval);
        }
        public async Task<List<PaymentApprovalResponse>> GetPendingApprovalsAsync()
        {
            var approvals = await _context.PaymentApprovals
                .Where(x => x.Status == PaymentApprovalStatus.Pending)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return approvals.Select(MapToResponse).ToList();
        }
        public async Task ApprovePaymentAsync(int approvalId, int reviewedByUserId)
        {
            var approval = await _context.PaymentApprovals
                .FirstOrDefaultAsync(x => x.Id == approvalId);

            if (approval == null)
                throw new InvalidOperationException("Payment approval request not found.");

            if (approval.Status != PaymentApprovalStatus.Pending)
                throw new InvalidOperationException("Only pending payment approvals can be approved.");

            // Maker-Checker Principle
            if (approval.RequestedByUserId == reviewedByUserId)
                throw new InvalidOperationException(
                    "Users cannot approve their own payment requests.");

            var fromAccount = await _context.Accounts
                .FirstOrDefaultAsync(x => x.Id == approval.FromAccountId);

            if (fromAccount == null)
                throw new InvalidOperationException("Sender account not found.");

            var toAccount = await _context.Accounts
                .FirstOrDefaultAsync(x => x.Id == approval.ToAccountId);

            if (toAccount == null)
                throw new InvalidOperationException("Receiver account not found.");

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
        }

        public async Task RejectPaymentAsync(
            int approvalId,
            int reviewedByUserId,
            string rejectionReason)
        {
            var approval = await _context.PaymentApprovals
                .FirstOrDefaultAsync(x => x.Id == approvalId);

            if (approval == null)
                throw new InvalidOperationException("Payment approval request not found.");

            if (approval.Status != PaymentApprovalStatus.Pending)
                throw new InvalidOperationException("Only pending payment approvals can be rejected.");

            approval.Status = PaymentApprovalStatus.Rejected;
            approval.ReviewedByUserId = reviewedByUserId;
            approval.ReviewedAt = DateTime.UtcNow;
            approval.RejectionReason = rejectionReason;

            await _context.SaveChangesAsync();
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
        private PaymentApprovalResponse MapToResponse(PaymentApproval approval)
        {
            return new PaymentApprovalResponse
            {
                Id = approval.Id,
                FromAccountId = approval.FromAccountId,
                ToAccountId = approval.ToAccountId,
                Amount = approval.Amount,
                Currency = approval.Currency,
                Description = approval.Description,
                Status = approval.Status.ToString(),
                RiskLevel = approval.RiskLevel.ToString(),
                ApprovalReason = approval.ApprovalReason,
                RequestedByUserId = approval.RequestedByUserId,
                ReviewedByUserId = approval.ReviewedByUserId,
                CreatedAt = approval.CreatedAt,
                ReviewedAt = approval.ReviewedAt,
                RejectionReason = approval.RejectionReason,
                RelatedTransactionId = approval.RelatedTransactionId
            };
        }
        public async Task<PaymentApprovalResponse?> GetByIdAsync(int approvalId)
        {
            var approval = await _context.PaymentApprovals
                .FirstOrDefaultAsync(x => x.Id == approvalId);
            if (approval == null)
                return null;
            return MapToResponse(approval);
        }
    }
}
