using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Account;

namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Service for account-related business operations.
/// Handles account creation, retrieval, and account balance management.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Opens a new account for a customer. Admin only; an optional opening deposit
    /// is recorded as a ledger transaction by the implementation.
    /// </summary>
    Task<AccountDto> OpenAccountAsync(CreateAccountRequest request, ActorContext actor, CancellationToken cancellationToken = default);

    /// <summary>Retrieves accounts with pagination, optionally filtered by customer.</summary>
    Task<(List<AccountDto> Accounts, int TotalCount)> GetAllAccountsAsync(
        int page = 1,
        int pageSize = 10,
        Guid? customerId = null,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves account details by ID including recent transactions.</summary>
    Task<AccountDetailDto> GetAccountDetailAsync(Guid accountId, int recentTransactionCount = 10, CancellationToken cancellationToken = default);

    /// <summary>Retrieves basic account information by ID.</summary>
    Task<AccountDto> GetAccountByIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>Retrieves all accounts for a specific customer.</summary>
    Task<List<AccountDto>> GetAccountsByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
}
