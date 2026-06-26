using DigitalBanking.Application.DTOs.Account;

namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Service for account-related business operations.
/// Handles account creation, retrieval, and account balance management.
/// </summary>
public interface IAccountService
{
    /// <summary>
    /// Opens a new account for a customer.
    /// </summary>
    /// <param name="request">Create account request with customer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Newly created account DTO.</returns>
    /// <exception cref="NotFoundException">If customer not found.</exception>
    Task<AccountDto> OpenAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all accounts with optional pagination.
    /// </summary>
    /// <param name="page">Page number (1-based). Default is 1.</param>
    /// <param name="pageSize">Number of items per page. Default is 10.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of account DTOs.</returns>
    Task<(List<AccountDto> Accounts, int TotalCount)> GetAllAccountsAsync(int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves account details by ID including recent transactions.
    /// </summary>
    /// <param name="accountId">Account ID.</param>
    /// <param name="recentTransactionCount">Number of recent transactions to include. Default is 10.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Account detail DTO with transactions.</returns>
    /// <exception cref="NotFoundException">If account not found.</exception>
    Task<AccountDetailDto> GetAccountDetailAsync(Guid accountId, int recentTransactionCount = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves basic account information by ID.
    /// </summary>
    /// <param name="accountId">Account ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Account DTO.</returns>
    /// <exception cref="NotFoundException">If account not found.</exception>
    Task<AccountDto> GetAccountByIdAsync(Guid accountId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all accounts for a specific customer.
    /// </summary>
    /// <param name="customerId">Customer ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of account DTOs for the customer.</returns>
    /// <exception cref="NotFoundException">If customer not found.</exception>
    Task<List<AccountDto>> GetAccountsByCustomerIdAsync(Guid customerId, CancellationToken cancellationToken = default);
}
