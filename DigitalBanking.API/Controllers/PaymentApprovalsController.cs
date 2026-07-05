using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Application.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentApprovalsController : ControllerBase
{
    private readonly IPaymentApprovalService _paymentApprovalService;

    public PaymentApprovalsController(IPaymentApprovalService paymentApprovalService)
    {
        _paymentApprovalService = paymentApprovalService;
    }

    [HttpPost]
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
            request.RequestedByUserId);

        return CreatedAtAction(
            nameof(GetById),
            new { id = approval.Id },
            ApiResponse<PaymentApprovalResponse>.SuccessWith(
                approval,
                "Payment approval request created successfully."));
    }

    [HttpGet("pending")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<PaymentApprovalResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPendingApprovals(
        CancellationToken cancellationToken = default)
    {
        var approvals = await _paymentApprovalService.GetPendingApprovalsAsync();

        return Ok(ApiResponse<IEnumerable<PaymentApprovalResponse>>.SuccessWith(approvals));
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentApprovalResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        int id,
        CancellationToken cancellationToken = default)
    {
        var approval = await _paymentApprovalService.GetByIdAsync(id);

        return Ok(ApiResponse<PaymentApprovalResponse>.SuccessWith(approval));
    }

    [HttpPost("{id:int}/approve")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ApprovePayment(
        int id,
        [FromBody] ApprovePaymentApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        await _paymentApprovalService.ApprovePaymentAsync(
            id,
            request.ReviewedByUserId);

        return NoContent();
    }

    [HttpPost("{id:int}/reject")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RejectPayment(
        int id,
        [FromBody] RejectPaymentApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        await _paymentApprovalService.RejectPaymentAsync(
            id,
            request.ReviewedByUserId,
            request.RejectionReason);

        return NoContent();
    }
}