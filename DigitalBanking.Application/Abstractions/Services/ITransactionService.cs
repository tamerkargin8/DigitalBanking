using DigitalBanking.Application.DTOs.Transaction;

namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Service for transaction-related business operations.
/// Handles deposits, withdrawals, transfers, and transaction history.
/// </summary>
public interface ITransactionService
{
    /// <summary>
    /// Deposits money into an account.
    /// Creates a transaction record and updates account balance.
    /// </summary>
    /// <param name="request">Deposit request with account ID and amount.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transaction DTO for the created deposit transaction.</returns>
    /// <exception cref="NotFoundException">If account not found.</exception>
    /// <exception cref="BusinessRuleException">If amount is invalid.</exception>
    /// <exception cref="ConcurrencyException">If concurrent modification detected.</exception>
    Task<TransactionDto> DepositAsync(DepositRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Withdraws money from an account.
    /// Creates a transaction record and updates account balance.
    /// </summary>
    /// <param name="request">Withdraw request with account ID and amount.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transaction DTO for the created withdraw transaction.</returns>
    /// <exception cref="NotFoundException">If account not found.</exception>
    /// <exception cref="BusinessRuleException">If amount is invalid or insufficient balance.</exception>
    /// <exception cref="ConcurrencyException">If concurrent modification detected.</exception>
    Task<TransactionDto> WithdrawAsync(WithdrawRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transfers money between two accounts.
    /// Creates two transaction records (withdrawal and deposit) and updates both account balances.
    /// </summary>
    /// <param name="request">Transfer request with source, destination, and amount.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transfer result containing both transaction records.</returns>
    /// <exception cref="NotFoundException">If either account not found.</exception>
    /// <exception cref="BusinessRuleException">If amount is invalid, insufficient balance, or same account.</exception>
    /// <exception cref="ConcurrencyException">If concurrent modification detected.</exception>
    Task<TransferResult> TransferAsync(TransferRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves transaction history for an account with pagination.
    /// </summary>
    /// <param name="accountId">Account ID.</param>
    /// <param name="page">Page number (1-based). Default is 1.</param>
    /// <param name="pageSize">Number of items per page. Default is 10.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transaction history DTO with pagination.</returns>
    /// <exception cref="NotFoundException">If account not found.</exception>
    Task<TransactionHistoryDto> GetTransactionHistoryAsync(Guid accountId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a single transaction by ID.
    /// </summary>
    /// <param name="transactionId">Transaction ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Transaction DTO.</returns>
    /// <exception cref="NotFoundException">If transaction not found.</exception>
    Task<TransactionDto> GetTransactionByIdAsync(Guid transactionId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a transfer operation containing both outgoing and incoming transaction records.
/// </summary>
public class TransferResult
{
    /// <summary>
    /// Transaction record for withdrawal from source account.
    /// </summary>
    public TransactionDto OutgoingTransaction { get; set; } = new();

    /// <summary>
    /// Transaction record for deposit into destination account.
    /// </summary>
    public TransactionDto IncomingTransaction { get; set; } = new();
}
