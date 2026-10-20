using AutoMapper;
using DigitalBanking.API.Extensions;
using DigitalBanking.API.Filters;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Transaction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Accounts API endpoint controller.
/// Route handlers enforce role policy; services re-check it from the JWT actor.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
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

    /// <summary>Opens a new account for a customer. Admin only.</summary>
    [HttpPost("open")]
    [Authorize(Roles = "Admin")]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<AccountDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> OpenAccount(
        [FromBody] OpenAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var createRequest = new CreateAccountRequest
        {
            CustomerId = request.CustomerId,
            Currency = request.Currency,
            InitialBalance = request.InitialBalance
        };

        var account = await _accountService.OpenAccountAsync(createRequest, User.GetActor(), cancellationToken);

        return Ok(ApiResponse<AccountDto>.SuccessWith(account, "Account opened successfully."));
    }

    /// <summary>Retrieves accounts with pagination and optional customer filter.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<AccountDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] Guid? customerId = null,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var (accounts, totalCount) = await _accountService.GetAllAccountsAsync(
            page, pageSize, customerId, cancellationToken);

        var paginated = new PaginatedResponse<AccountDto>(accounts, page, pageSize, totalCount);
        return Ok(ApiResponse<PaginatedResponse<AccountDto>>.SuccessWith(paginated));
    }

    /// <summary>Retrieves account details by ID with recent transactions.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AccountDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var account = await _accountService.GetAccountDetailAsync(id, recentTransactionCount: 10, cancellationToken);
        return Ok(ApiResponse<AccountDetailDto>.SuccessWith(account));
    }

    /// <summary>
    /// Direct transfer. Admin only and limited to low-risk amounts; larger amounts
    /// must go through POST /api/paymentapprovals (maker-checker).
    /// </summary>
    [HttpPost("transfer")]
    [Authorize(Roles = "Admin")]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<TransferResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Transfer(
        [FromBody] TransferRequest request,
        CancellationToken cancellationToken = default)
    {
        var result = await _transactionService.TransferAsync(request, User.GetActor(), cancellationToken);
        var response = _mapper.Map<TransferResultDto>(result);

        return Ok(ApiResponse<TransferResultDto>.SuccessWith(response, "Transfer completed successfully."));
    }
}
