using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Transaction;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Enums;
using DigitalBanking.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Transactions API endpoint controller.
/// Handles deposit, withdraw, transaction history, and reporting.
/// </summary>
[ApiController]
[Route("api/[controller]")]
//[Authorize]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    private readonly BankDbContext _db; // Used for reporting endpoints only

    public TransactionsController(ITransactionService transactionService, BankDbContext db)
    {
        _transactionService = transactionService;
        _db = db;
    }

    /// <summary>
    /// Deposits money into an account.
    /// POST /api/transactions/deposit
    /// </summary>
    [HttpPost("deposit")]
    [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Deposit([FromBody] DepositRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var transaction = await _transactionService.DepositAsync(request, cancellationToken);
            return Ok(ApiResponse<TransactionDto>.SuccessWith(transaction, "Deposit completed successfully."));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse.Failure(ex.Message));
        }
        catch (ConcurrencyException ex)
        {
            return Conflict(ApiResponse.Failure(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(ApiResponse.Failure(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Failure(ex.Message));
        }
    }

    /// <summary>
    /// Withdraws money from an account.
    /// POST /api/transactions/withdraw
    /// </summary>
    [HttpPost("withdraw")]
    [ProducesResponseType(typeof(ApiResponse<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Withdraw([FromBody] WithdrawRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var transaction = await _transactionService.WithdrawAsync(request, cancellationToken);
            return Ok(ApiResponse<TransactionDto>.SuccessWith(transaction, "Withdrawal completed successfully."));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse.Failure(ex.Message));
        }
        catch (ConcurrencyException ex)
        {
            return Conflict(ApiResponse.Failure(ex.Message));
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(ApiResponse.Failure(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Failure(ex.Message));
        }
    }

    /// <summary>
    /// Retrieves transaction history for an account with pagination.
    /// GET /api/transactions/by-account/{accountId}?page=1&pageSize=10
    /// </summary>
    [HttpGet("by-account/{accountId:guid}")]
    [ProducesResponseType(typeof(ApiResponse<TransactionHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByAccount(Guid accountId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var history = await _transactionService.GetTransactionHistoryAsync(accountId, page, pageSize, cancellationToken);
            return Ok(ApiResponse<TransactionHistoryDto>.SuccessWith(history));
        }
        catch (NotFoundException ex)
        {
            return NotFound(ApiResponse.Failure(ex.Message));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Failure(ex.Message));
        }
    }

    /// <summary>
    /// Retrieves transactions for date range reporting.
    /// GET /api/transactions/statement/{accountId}?from=2026-02-01&to=2026-02-28
    /// </summary>
    [HttpGet("statement/{accountId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetStatement(Guid accountId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (to < from)
            return BadRequest(ApiResponse.Failure("'to' date must be >= 'from' date."));

        var list = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId
                        && t.CreatedDate >= from
                        && t.CreatedDate <= to)
            .OrderBy(t => t.CreatedDate)
            .Select(t => new
            {
                t.Id,
                t.Type,
                t.Amount,
                t.BalanceAfter,
                t.Description,
                t.CreatedDate
            })
            .ToListAsync();

        return Ok(new
        {
            accountId,
            from,
            to,
            count = list.Count,
            transactions = list
        });
    }

    /// <summary>
    /// Retrieves daily transaction summary for date range.
    /// GET /api/transactions/daily-summary/{accountId}?from=2026-02-01&to=2026-03-01
    /// </summary>
    [HttpGet("daily-summary/{accountId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetDailySummary(Guid accountId, [FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        if (to < from)
            return BadRequest(ApiResponse.Failure("'to' date must be >= 'from' date."));

        var summary = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.AccountId == accountId
                        && t.CreatedDate >= from
                        && t.CreatedDate <= to)
            .GroupBy(t => t.CreatedDate.Date)
            .Select(g => new
            {
                date = g.Key,
                depositTotal = g.Where(x => x.Type == TransactionType.Deposit).Sum(x => x.Amount),
                withdrawTotal = g.Where(x => x.Type == TransactionType.Withdraw).Sum(x => x.Amount),
                count = g.Count()
            })
            .OrderBy(x => x.date)
            .ToListAsync();

        return Ok(new
        {
            accountId,
            from,
            to,
            days = summary.Count,
            summary
        });
    }

    /// <summary>
    /// Retrieves top accounts by transaction volume.
    /// GET /api/transactions/top-accounts?from=2026-02-01&to=2026-03-01&take=5
    /// </summary>
    [HttpGet("top-accounts")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetTopAccounts([FromQuery] DateTime from, [FromQuery] DateTime to, [FromQuery] int take = 5)
    {
        if (to < from)
            return BadRequest(ApiResponse.Failure("'to' date must be >= 'from' date."));

        take = Math.Clamp(take, 1, 50);

        var top = await _db.Transactions
            .AsNoTracking()
            .Where(t => t.CreatedDate >= from && t.CreatedDate <= to)
            .GroupBy(t => t.AccountId)
            .Select(g => new
            {
                accountId = g.Key,
                transactionCount = g.Count(),
                depositTotal = g.Where(x => x.Type == TransactionType.Deposit).Sum(x => x.Amount),
                withdrawTotal = g.Where(x => x.Type == TransactionType.Withdraw).Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.transactionCount)
            .Take(take)
            .ToListAsync();

        return Ok(top);
    }
}
