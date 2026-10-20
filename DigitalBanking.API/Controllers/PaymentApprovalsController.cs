using DigitalBanking.API.Extensions;
using DigitalBanking.API.Filters;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Maker-checker payment approval endpoints. Requester and reviewer identities
/// are taken from the JWT actor, never from the request body.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class PaymentApprovalsController : ControllerBase
{
    private readonly IPaymentApprovalService _paymentApprovalService;

    public PaymentApprovalsController(IPaymentApprovalService paymentApprovalService)
    {
        _paymentApprovalService = paymentApprovalService;
    }

    /// <summary>
    /// Creates an approval request. Low-risk amounts are auto-approved immediately;
    /// medium/high/critical amounts stay Pending for a different Admin.
    /// </summary>
    [HttpPost]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<PaymentApprovalResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateApprovalRequest(
        [FromBody] CreatePaymentApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        var approval = await _paymentApprovalService.CreateApprovalRequestAsync(
            request.FromAccountId,
            request.ToAccountId,
            request.Amount,
            request.Description,
            User.GetActor(),
            cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = approval.Id },
            ApiResponse<PaymentApprovalResponse>.SuccessWith(
                approval,
                approval.Status == "AutoApproved"
                    ? "Low risk payment auto-approved and applied."
                    : "Payment approval request created successfully."));
    }

    [HttpGet("pending")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PaymentApprovalResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingApprovals(CancellationToken cancellationToken = default)
    {
        var approvals = await _paymentApprovalService.GetPendingApprovalsAsync(cancellationToken);
        return Ok(ApiResponse<IEnumerable<PaymentApprovalResponse>>.SuccessWith(approvals));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentApprovalResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
    {
        var approval = await _paymentApprovalService.GetByIdAsync(id, cancellationToken);
        return Ok(ApiResponse<PaymentApprovalResponse>.SuccessWith(approval));
    }

    [HttpPost("{id:int}/approve")]
    [Idempotent]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ApprovePayment(int id, CancellationToken cancellationToken = default)
    {
        await _paymentApprovalService.ApprovePaymentAsync(id, User.GetActor(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/reject")]
    [Idempotent]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RejectPayment(
        int id,
        [FromBody] RejectPaymentApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        await _paymentApprovalService.RejectPaymentAsync(
            id,
            User.GetActor(),
            request.RejectionReason,
            cancellationToken);

        return NoContent();
    }
}
