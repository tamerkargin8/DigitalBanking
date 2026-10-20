using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.ApprovalRequests;
using DigitalBanking.Application.DTOs.PaymentApprovals;

namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Approval-request workflow. No customer financial request is auto-approved:
/// every transfer/deposit/withdrawal/account opening starts as Pending and is
/// applied only by an Owner or an authorized DelegatedApprover (never the maker).
/// </summary>
public interface IPaymentApprovalService
{
    Task<PaymentApprovalResponse> CreateApprovalRequestAsync(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string? description,
        ActorContext actor,
        bool isDemo = false,
        CancellationToken cancellationToken = default);

    Task<PaymentApprovalResponse> CreateCustomerTransferRequestAsync(
        Guid fromAccountId,
        Guid toAccountId,
        decimal amount,
        string? description,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<PaymentApprovalResponse> CreateCustomerOperationRequestAsync(
        CreateCustomerOperationRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<PaymentApprovalResponse> CreateOperationalRequestAsync(
        CreateOperationalRequest request,
        ActorContext actor,
        CancellationToken cancellationToken = default);

    Task<List<PaymentApprovalResponse>> GetPendingApprovalsAsync(ActorContext actor, CancellationToken cancellationToken = default);

    Task<List<PaymentApprovalResponse>> GetQueueAsync(
        ActorContext actor,
        string? status,
        string? requestType,
        string? currency,
        string? scope,
        CancellationToken cancellationToken = default);

    Task<PaymentApprovalResponse> GetByIdAsync(int approvalId, ActorContext actor, CancellationToken cancellationToken = default);

    Task ApprovePaymentAsync(int approvalId, ActorContext reviewer, CancellationToken cancellationToken = default);

    Task RejectPaymentAsync(int approvalId, ActorContext reviewer, string rejectionReason, CancellationToken cancellationToken = default);

    Task CancelAsync(int approvalId, ActorContext actor, CancellationToken cancellationToken = default);
}
