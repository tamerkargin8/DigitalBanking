using DigitalBanking.API.Extensions;
using DigitalBanking.API.Filters;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Transaction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Transactions API endpoint controller.
/// Money movements are Admin-only; reporting is available to authenticated users.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;

    public TransactionsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    /// <summary>Deposits money into an account. Admin only.</summary>
    [HttpPost("deposit")]
    [Authorize(Roles = "Admin")]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Deposit(
        [FromBody] DepositRequest request,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _transactionService.DepositAsync(request, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<TransactionDto>.SuccessWith(transaction, "Deposit completed successfully."));
    }

    /// <summary>Withdraws money from an account. Admin only.</summary>
    [HttpPost("withdraw")]
    [Authorize(Roles = "Admin")]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Withdraw(
        [FromBody] WithdrawRequest request,
        CancellationToken cancellationToken = default)
    {
        var transaction = await _transactionService.WithdrawAsync(request, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<TransactionDto>.SuccessWith(transaction, "Withdrawal completed successfully."));
    }

    /// <summary>Retrieves transaction history for an account with pagination.</summary>
    [HttpGet("by-account/{accountId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TransactionHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByAccount(
        Guid accountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var history = await _transactionService.GetTransactionHistoryAsync(
            accountId, page, pageSize, cancellationToken);

        return Ok(ApiResponse<TransactionHistoryDto>.SuccessWith(history));
    }

    /// <summary>Date-bounded account statement. A date-only 'to' is inclusive.</summary>
    [HttpGet("statement/{accountId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TransactionStatementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetStatement(
        Guid accountId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken = default)
    {
        var statement = await _transactionService.GetStatementAsync(accountId, from, to, cancellationToken);
        return Ok(ApiResponse<TransactionStatementResponse>.SuccessWith(statement));
    }

    /// <summary>Daily debit/credit summary for one account.</summary>
    [HttpGet("daily-summary/{accountId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<DailyTransactionSummaryResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDailySummary(
        Guid accountId,
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        CancellationToken cancellationToken = default)
    {
        var summary = await _transactionService.GetDailySummaryAsync(accountId, from, to, cancellationToken);
        return Ok(ApiResponse<DailyTransactionSummaryResponse>.SuccessWith(summary));
    }

    /// <summary>Top accounts by transaction count for one currency.</summary>
    [HttpGet("top-accounts")]
    [ProducesResponseType(typeof(ApiResponse<List<TopAccountTransactionResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTopAccounts(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] int take = 5,
        [FromQuery] string currency = "TRY",
        CancellationToken cancellationToken = default)
    {
        var top = await _transactionService.GetTopAccountsAsync(from, to, take, currency, cancellationToken);
        return Ok(ApiResponse<List<TopAccountTransactionResponse>>.SuccessWith(top));
    }
}
