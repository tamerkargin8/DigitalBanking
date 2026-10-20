using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.PaymentApprovals;

namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Maker-checker payment approval workflow. User identities come from the
/// authenticated caller (<see cref="ActorContext"/>), never from the request body.
/// </summary>
public interface IPaymentApprovalService
{
    Task<PaymentApprovalResponse> CreateApprovalRequestAsync(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string? description,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<List<PaymentApprovalResponse>> GetPendingApprovalsAsync(CancellationToken cancellationToken = default);

    Task<PaymentApprovalResponse> GetByIdAsync(int approvalId, CancellationToken cancellationToken = default);

    Task ApprovePaymentAsync(int approvalId, ActorContext reviewer, CancellationToken cancellationToken = default);

    Task RejectPaymentAsync(int approvalId, ActorContext reviewer, string rejectionReason, CancellationToken cancellationToken = default);
}
