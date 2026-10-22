using DigitalBanking.API.Extensions;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Infrastructure.Services;
using DigitalBanking.Application.DTOs.Accounting;
using DigitalBanking.Application.DTOs.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Read-only double-entry accounting. Journals are append-only: there is no
/// update/delete endpoint. Baseline is an explicit Owner action and never runs
/// automatically for legacy accounts.
/// </summary>
[ApiController]
[Route("api/accounting")]
[Authorize(Roles = "Owner,DelegatedApprover")]
public class AccountingController : ControllerBase
{
    private readonly IAccountingService _accounting;

    public AccountingController(IAccountingService accounting)
    {
        _accounting = accounting;
    }

    [HttpGet("trial-balance")]
    [ProducesResponseType(typeof(ApiResponse<TrialBalanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrialBalance(
        [FromQuery] string? currency = null,
        CancellationToken cancellationToken = default)
    {
        var data = await _accounting.GetTrialBalanceAsync(currency, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<TrialBalanceDto>.SuccessWith(
            data,
            data.IsBalanced
                ? "Mizan dengeli."
                : "Mizan dengesiz; journal kayıtları incelenmeli."));
    }

    [HttpGet("journal-entries")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<JournalEntrySummaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetEntries(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? currency = null,
        [FromQuery] Guid? accountId = null,
        [FromQuery] string? sourceOperation = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            return BadRequest(ApiResponse.Failure("page en az 1 olmalıdır."));
        if (pageSize is < 1 or > AccountingService.MaxPageSize)
            return BadRequest(ApiResponse.Failure($"pageSize 1-{AccountingService.MaxPageSize} aralığında olmalıdır."));

        var data = await _accounting.GetEntriesAsync(
            page, pageSize, currency, accountId, sourceOperation, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<PaginatedResponse<JournalEntrySummaryDto>>.SuccessWith(data));
    }

    [HttpGet("journal-entries/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<JournalEntryDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEntry(Guid id, CancellationToken cancellationToken)
    {
        var data = await _accounting.GetEntryAsync(id, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<JournalEntryDetailDto>.SuccessWith(data));
    }

    [HttpGet("baseline/preview")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse<BaselinePreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewBaseline(CancellationToken cancellationToken)
    {
        var data = await _accounting.PreviewBaselineAsync(User.GetActor(), cancellationToken);
        return Ok(ApiResponse<BaselinePreviewDto>.SuccessWith(data));
    }

    [HttpPost("baseline/post")]
    [Authorize(Roles = "Owner")]
    [ProducesResponseType(typeof(ApiResponse<BaselinePostResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PostBaseline(CancellationToken cancellationToken)
    {
        var data = await _accounting.PostBaselineAsync(User.GetActor(), cancellationToken);
        return Ok(ApiResponse<BaselinePostResultDto>.SuccessWith(data, data.Message));
    }
}
