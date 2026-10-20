using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.CustomerPortal;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Application.DTOs.Transaction;

namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Customer self-service. Every method resolves ownership from the JWT actor;
/// a non-owned resource is reported as NotFound (no id enumeration).
/// </summary>
public interface ICustomerPortalService
{
    Task<CustomerProfileDto> GetProfileAsync(ActorContext actor, CancellationToken cancellationToken = default);
    Task<List<AccountDto>> GetAccountsAsync(ActorContext actor, CancellationToken cancellationToken = default);
    Task<AccountDetailDto> GetAccountAsync(Guid accountId, ActorContext actor, CancellationToken cancellationToken = default);
    Task<TransactionHistoryDto> GetTransactionsAsync(Guid accountId, int page, int pageSize, ActorContext actor, CancellationToken cancellationToken = default);
    Task<List<BeneficiaryDto>> GetBeneficiariesAsync(ActorContext actor, CancellationToken cancellationToken = default);
    Task<List<PaymentApprovalResponse>> GetPaymentsAsync(ActorContext actor, CancellationToken cancellationToken = default);
    Task<PaymentApprovalResponse> GetPaymentAsync(int paymentId, ActorContext actor, CancellationToken cancellationToken = default);
    Task<PaymentApprovalResponse> CreateTransferRequestAsync(CreateCustomerTransferRequest request, ActorContext actor, CancellationToken cancellationToken = default);
}
