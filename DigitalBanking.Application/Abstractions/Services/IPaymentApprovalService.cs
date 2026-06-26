using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Domain.Enums;


namespace DigitalBanking.Application.Abstractions.Services;

public interface IPaymentApprovalService
{
    Task<PaymentApprovalResponse> CreateApprovalRequestAsync(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string description,
        int requestedByUserId);

    Task<List<PaymentApprovalResponse>> GetPendingApprovalsAsync();

    Task<PaymentApprovalResponse?> GetByIdAsync(int approvalId);

    Task ApprovePaymentAsync(
        int approvalId,
        int reviewedByUserId);

    Task RejectPaymentAsync(
        int approvalId,
        int reviewedByUserId,
        string rejectionReason);
}