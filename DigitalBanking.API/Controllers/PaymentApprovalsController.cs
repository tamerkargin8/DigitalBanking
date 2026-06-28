using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.PaymentApprovals;
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
    public async Task<IActionResult> CreateApprovalRequest(
        [FromBody] CreatePaymentApprovalRequest request)
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
            approval);
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingApprovals()
    {
        var approvals = await _paymentApprovalService.GetPendingApprovalsAsync();

        return Ok(approvals);
    }

    [HttpPost("{id:int}/reject")]
    public async Task<IActionResult> RejectPayment(
        int id,
        [FromBody] RejectPaymentApprovalRequest request)
    {
        await _paymentApprovalService.RejectPaymentAsync(
            id,
            request.ReviewedByUserId,
            request.RejectionReason);

        return NoContent();
    }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var approval = await _paymentApprovalService.GetByIdAsync(id);

        if (approval == null)
            return NotFound();

        return Ok(approval);
    }

    [HttpPost("{id:int}/approve")]
    public async Task<IActionResult> ApprovePayment(
    int id,
    [FromBody] ApprovePaymentApprovalRequest request)
    {
        await _paymentApprovalService.ApprovePaymentAsync(
            id,
            request.ReviewedByUserId);

        return NoContent();
    }
}