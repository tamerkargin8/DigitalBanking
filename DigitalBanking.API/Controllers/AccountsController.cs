using AutoMapper;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.Accounts;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Transaction;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Accounts API endpoint controller.
/// Handles account creation, retrieval, and account-level operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
//[Authorize]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly ITransactionService _transactionService;
    private readonly IMapper _mapper;

    public AccountsController(
        IAccountService accountService,
        ITransactionService transactionService,
        IMapper mapper)
    {
        _accountService = accountService;
        _transactionService = transactionService;
        _mapper = mapper;
    }

    /// <summary>
    /// Opens a new account for a customer.
    /// POST /api/accounts/open
    /// </summary>
    [HttpPost("open")]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> OpenAccount(
    [FromBody] OpenAccountRequest request,
    CancellationToken cancellationToken = default)
    {
        var createRequest = _mapper.Map<CreateAccountRequest>(request);

        var account = await _accountService.OpenAccountAsync(
            createRequest,
            cancellationToken);

        return Ok(ApiResponse<AccountDto>.SuccessWith(
            account,
            "Account opened successfully."));
    }

    /// <summary>
    /// Retrieves all accounts with pagination.
    /// GET /api/accounts?page=1&pageSize=10
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<AccountDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var (accounts, totalCount) = await _accountService.GetAllAccountsAsync(
            page,
            pageSize,
            cancellationToken);

        var paginated = new PaginatedResponse<AccountDto>(
            accounts,
            page,
            pageSize,
            totalCount);

        return Ok(ApiResponse<PaginatedResponse<AccountDto>>.SuccessWith(paginated));
    }

    /// <summary>
    /// Retrieves account details by ID with recent transactions.
    /// GET /api/accounts/{id}
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AccountDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var account = await _accountService.GetAccountDetailAsync(
            id,
            recentTransactionCount: 10,
            cancellationToken);

        return Ok(ApiResponse<AccountDetailDto>.SuccessWith(account));
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
    public async Task<IActionResult> Transfer(
        [FromBody] TransferRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _transactionService.TransferAsync(request, cancellationToken);

        var response = _mapper.Map<TransferResultDto>(result);

        return Ok(ApiResponse<TransferResultDto>.SuccessWith(
            response,
            "Transfer completed successfully."));
    }
}