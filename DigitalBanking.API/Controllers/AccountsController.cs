using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Transaction;
using DigitalBanking.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Accounts API endpoint controller.
/// Handles account creation, retrieval, and account-level operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly ITransactionService _transactionService;

    public AccountsController(IAccountService accountService, ITransactionService transactionService)
    {
        _accountService = accountService;
        _transactionService = transactionService;
    }

    /// <summary>
    /// Opens a new account for a customer.
    /// POST /api/accounts/open
    /// </summary>
    [HttpPost("open")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> OpenAccount([FromBody] OpenAccountRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var createRequest = new CreateAccountRequest { CustomerId = request.CustomerId };
            var account = await _accountService.OpenAccountAsync(createRequest, cancellationToken);
            return Ok(ApiResponse<AccountDto>.SuccessWith(account, "Account opened successfully."));
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
    /// Retrieves all accounts with pagination.
    /// GET /api/accounts?page=1&pageSize=10
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<AccountDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken cancellationToken = default)
    {
        try
        {
            var (accounts, totalCount) = await _accountService.GetAllAccountsAsync(page, pageSize, cancellationToken);
            var paginated = new PaginatedResponse<AccountDto>(accounts, page, pageSize, totalCount);
            return Ok(ApiResponse<PaginatedResponse<AccountDto>>.SuccessWith(paginated));
        }
        catch (Exception ex)
        {
            return BadRequest(ApiResponse.Failure(ex.Message));
        }
    }

    /// <summary>
    /// Retrieves account details by ID with recent transactions.
    /// GET /api/accounts/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AccountDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var account = await _accountService.GetAccountDetailAsync(id, recentTransactionCount: 10, cancellationToken);
            return Ok(ApiResponse<AccountDetailDto>.SuccessWith(account));
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
    /// Transfers money between two accounts.
    /// POST /api/accounts/transfer
    /// </summary>
    [HttpPost("transfer")]
    [ProducesResponseType(typeof(ApiResponse<TransferResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Transfer([FromBody] TransferRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _transactionService.TransferAsync(request, cancellationToken);
            return Ok(ApiResponse<TransferResultDto>.SuccessWith(
                new TransferResultDto
                {
                    OutgoingTransaction = result.OutgoingTransaction,
                    IncomingTransaction = result.IncomingTransaction
                },
                "Transfer completed successfully."));
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
}

/// <summary>
/// Request model for opening a new account.
/// </summary>
public record OpenAccountRequest(Guid CustomerId);

/// <summary>
/// DTO for transfer operation result containing both transaction records.
/// </summary>
public class TransferResultDto
{
    public TransactionDto? OutgoingTransaction { get; set; }
    public TransactionDto? IncomingTransaction { get; set; }
}