using DigitalBanking.Application.Common;
using DigitalBanking.Application.DTOs.Transaction;

namespace DigitalBanking.Application.Abstractions.Services;

/// <summary>
/// Service for transaction-related business operations.
/// Handles deposits, withdrawals, transfers, transaction history and reports.
/// </summary>
public interface ITransactionService
{
    /// <summary>Deposits money into an account (Admin only).</summary>
    Task<TransactionDto> DepositAsync(DepositRequest request, ActorContext actor, CancellationToken cancellationToken = default);

    /// <summary>Withdraws money from an account (Admin only).</summary>
    Task<TransactionDto> WithdrawAsync(WithdrawRequest request, ActorContext actor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Transfers money directly between accounts (Admin only, below the configured
    /// direct-transfer limit). Larger or riskier payments must use the approval flow.
    /// </summary>
    Task<TransferResult> TransferAsync(TransferRequest request, ActorContext actor, CancellationToken cancellationToken = default);

    /// <summary>Retrieves transaction history for an account with pagination.</summary>
    Task<TransactionHistoryDto> GetTransactionHistoryAsync(Guid accountId, int page = 1, int pageSize = 10, CancellationToken cancellationToken = default);

    /// <summary>Retrieves a single transaction by ID.</summary>
    Task<TransactionDto> GetTransactionByIdAsync(Guid transactionId, CancellationToken cancellationToken = default);

    /// <summary>Date-bounded account statement with an inclusive end-of-day <c>to</c>.</summary>
    Task<TransactionStatementResponse> GetStatementAsync(Guid accountId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Daily debit/credit summary for one account in a date range.</summary>
    Task<DailyTransactionSummaryResponse> GetDailySummaryAsync(Guid accountId, DateTime from, DateTime to, CancellationToken cancellationToken = default);

    /// <summary>Top accounts by transaction count for one currency in a date range.</summary>
    Task<List<TopAccountTransactionResponse>> GetTopAccountsAsync(DateTime from, DateTime to, int take, string? currency, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a transfer operation containing both outgoing and incoming transaction records.
/// </summary>
public class TransferResult
{
    public TransactionDto OutgoingTransaction { get; set; } = new();
    public TransactionDto IncomingTransaction { get; set; } = new();
}
