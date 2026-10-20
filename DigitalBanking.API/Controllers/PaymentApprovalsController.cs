using DigitalBanking.API.Extensions;
using DigitalBanking.API.Filters;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.ApprovalRequests;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.CustomerPortal;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Maker-checker approval endpoints. Roles and delegation limits are checked from
/// the current database row inside the service; a stale JWT cannot keep authority.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PaymentApprovalsController : ControllerBase
{
    private readonly IPaymentApprovalService _paymentApprovalService;
    private readonly IPaymentTimelineService _timelineService;

    public PaymentApprovalsController(
        IPaymentApprovalService paymentApprovalService,
        IPaymentTimelineService timelineService)
    {
        _paymentApprovalService = paymentApprovalService;
        _timelineService = timelineService;
    }

    /// <summary>Owner-only transfer request (legacy transfer shape).</summary>
    [HttpPost]
    [Idempotent]
    public async Task<IActionResult> CreateApprovalRequest(
        [FromBody] CreatePaymentApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        var approval = await _paymentApprovalService.CreateApprovalRequestAsync(
            request.FromAccountId, request.ToAccountId, request.Amount, request.Description,
            User.GetActor(), cancellationToken: cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = approval.Id },
            ApiResponse<PaymentApprovalResponse>.SuccessWith(approval, "Talep oluşturuldu; onay bekliyor."));
    }

    /// <summary>Owner-only request creation for transfer/deposit/withdrawal/account opening.</summary>
    [HttpPost("requests")]
    [Idempotent]
    public async Task<IActionResult> CreateOperationalRequest(
        [FromBody] CreateOperationalRequest request,
        CancellationToken cancellationToken = default)
    {
        var approval = await _paymentApprovalService.CreateOperationalRequestAsync(
            request, User.GetActor(), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = approval.Id },
            ApiResponse<PaymentApprovalResponse>.SuccessWith(approval, "Talep oluşturuldu; onay bekliyor."));
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingApprovals(CancellationToken cancellationToken = default)
    {
        var approvals = await _paymentApprovalService.GetPendingApprovalsAsync(User.GetActor(), cancellationToken);
        return Ok(ApiResponse<IEnumerable<PaymentApprovalResponse>>.SuccessWith(approvals));
    }

    [HttpGet("queue")]
    public async Task<IActionResult> GetQueue(
        [FromQuery] string? status = "Pending",
        [FromQuery] string? requestType = null,
        [FromQuery] string? currency = null,
        [FromQuery] string? scope = null,
        CancellationToken cancellationToken = default)
    {
        var approvals = await _paymentApprovalService.GetQueueAsync(
            User.GetActor(), status, requestType, currency, scope, cancellationToken);
        return Ok(ApiResponse<List<PaymentApprovalResponse>>.SuccessWith(approvals));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken = default)
    {
        var approval = await _paymentApprovalService.GetByIdAsync(id, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<PaymentApprovalResponse>.SuccessWith(approval));
    }

    [HttpGet("{id:int}/timeline")]
    public async Task<IActionResult> GetTimeline(int id, CancellationToken cancellationToken = default)
    {
        await _paymentApprovalService.GetByIdAsync(id, User.GetActor(), cancellationToken);
        var timeline = await _timelineService.GetTimelineAsync(id, cancellationToken);
        return Ok(ApiResponse<PaymentTimelineDto>.SuccessWith(timeline));
    }

    [HttpGet("{id:int}/receipt")]
    public async Task<IActionResult> GetReceipt(int id, CancellationToken cancellationToken = default)
    {
        await _paymentApprovalService.GetByIdAsync(id, User.GetActor(), cancellationToken);
        var receipt = await _timelineService.GetReceiptAsync(id, cancellationToken);
        return Ok(ApiResponse<PaymentReceiptDto>.SuccessWith(receipt));
    }

    [HttpPost("{id:int}/approve")]
    [Idempotent]
    public async Task<IActionResult> ApprovePayment(int id, CancellationToken cancellationToken = default)
    {
        await _paymentApprovalService.ApprovePaymentAsync(id, User.GetActor(), cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/reject")]
    [Idempotent]
    public async Task<IActionResult> RejectPayment(
        int id,
        [FromBody] RejectPaymentApprovalRequest request,
        CancellationToken cancellationToken = default)
    {
        await _paymentApprovalService.RejectPaymentAsync(
            id, User.GetActor(), request.RejectionReason, cancellationToken);
        return NoContent();
    }

    [HttpPost("{id:int}/cancel")]
    [Idempotent]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken = default)
    {
        await _paymentApprovalService.CancelAsync(id, User.GetActor(), cancellationToken);
        return NoContent();
    }
}
