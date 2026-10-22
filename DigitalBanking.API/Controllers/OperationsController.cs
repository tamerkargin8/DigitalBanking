using DigitalBanking.API.Extensions;
using DigitalBanking.API.Filters;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Operations;
using DigitalBanking.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Operations control centre: pending age/SLA, filters, reconciliation gaps and
/// safe assignment. Read-only except assignment; assignment never grants rights.
/// </summary>
[ApiController]
[Route("api/operations")]
[Authorize(Roles = "Owner,DelegatedApprover")]
public class OperationsController : ControllerBase
{
    private readonly IOperationsService _operations;

    public OperationsController(IOperationsService operations)
    {
        _operations = operations;
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(ApiResponse<OperationsSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var data = await _operations.GetSummaryAsync(User.GetActor(), cancellationToken);
        return Ok(ApiResponse<OperationsSummaryDto>.SuccessWith(data));
    }

    [HttpGet("approvals")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<OperationsApprovalRowDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetApprovals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null,
        [FromQuery] string? requestType = null,
        [FromQuery] string? currency = null,
        [FromQuery] string? age = null,
        [FromQuery] string? assignment = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            return BadRequest(ApiResponse.Failure("page en az 1 olmalıdır."));
        if (pageSize is < 1 or > OperationsService.MaxPageSize)
            return BadRequest(ApiResponse.Failure($"pageSize 1-{OperationsService.MaxPageSize} aralığında olmalıdır."));

        var data = await _operations.GetApprovalsAsync(
            page, pageSize, status, requestType, currency, age, assignment,
            User.GetActor(), cancellationToken);
        return Ok(ApiResponse<PaginatedResponse<OperationsApprovalRowDto>>.SuccessWith(data));
    }

    [HttpGet("reconciliation")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<OperationsReconciliationItemDto>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetReconciliation(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? currency = null,
        [FromQuery] string? exceptionFilter = null,
        CancellationToken cancellationToken = default)
    {
        if (page < 1)
            return BadRequest(ApiResponse.Failure("page en az 1 olmalıdır."));
        if (pageSize is < 1 or > OperationsService.MaxPageSize)
            return BadRequest(ApiResponse.Failure($"pageSize 1-{OperationsService.MaxPageSize} aralığında olmalıdır."));

        var data = await _operations.GetReconciliationAsync(
            page, pageSize, currency, exceptionFilter, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<PaginatedResponse<OperationsReconciliationItemDto>>.SuccessWith(data));
    }

    [HttpPost("approvals/{id:int}/assignment")]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<OperationsAssignmentResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Assign(
        int id, [FromBody] AssignApprovalRequest request, CancellationToken cancellationToken)
    {
        var data = await _operations.AssignApprovalAsync(
            id, request?.AssignedToUserId, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<OperationsAssignmentResultDto>.SuccessWith(data, data.Message));
    }
}
