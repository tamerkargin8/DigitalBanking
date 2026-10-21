using DigitalBanking.API.Extensions;
using DigitalBanking.API.Filters;
using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.ApprovalRequests;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.CustomerPortal;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Application.DTOs.Transaction;
using DigitalBanking.Application.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.API.Controllers;

/// <summary>
/// Customer self-service endpoints. Ownership is enforced server-side with joins
/// from the JWT user id; ids supplied by the browser are never trusted on their own.
/// </summary>
[ApiController]
[Route("api/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly ICustomerPortalService _portal;
    private readonly ICustomerAccessService _access;
    private readonly IPaymentTimelineService _timeline;
    private readonly IPaymentApprovalService _payments;

    public MeController(
        ICustomerPortalService portal,
        ICustomerAccessService access,
        IPaymentTimelineService timeline,
        IPaymentApprovalService payments)
    {
        _portal = portal;
        _access = access;
        _timeline = timeline;
        _payments = payments;
    }

    [HttpGet("profile")]
    [ProducesResponseType(typeof(ApiResponse<CustomerProfileDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var profile = await _portal.GetProfileAsync(User.GetActor(), cancellationToken);
        return Ok(ApiResponse<CustomerProfileDto>.SuccessWith(profile));
    }

    [HttpGet("accounts")]
    [ProducesResponseType(typeof(ApiResponse<List<AccountDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccounts(CancellationToken cancellationToken)
    {
        var accounts = await _portal.GetAccountsAsync(User.GetActor(), cancellationToken);
        return Ok(ApiResponse<List<AccountDto>>.SuccessWith(accounts));
    }

    [HttpGet("accounts/{id:guid}")]
    [ProducesResponseType(typeof(ApiResponse<AccountDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAccount(Guid id, CancellationToken cancellationToken)
    {
        var account = await _portal.GetAccountAsync(id, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<AccountDetailDto>.SuccessWith(account));
    }

    [HttpPut("accounts/{id:guid}/name")]
    [ProducesResponseType(typeof(ApiResponse<AccountDetailDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RenameAccount(
        Guid id,
        [FromBody] RenameAccountRequest request,
        CancellationToken cancellationToken)
    {
        var account = await _portal.RenameAccountAsync(id, request.AccountName, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<AccountDetailDto>.SuccessWith(account, "Hesap adı güncellendi."));
    }

    [HttpGet("accounts/{id:guid}/transactions")]
    [ProducesResponseType(typeof(ApiResponse<TransactionHistoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var history = await _portal.GetTransactionsAsync(id, page, pageSize, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<TransactionHistoryDto>.SuccessWith(history));
    }

    [HttpGet("beneficiaries")]
    [ProducesResponseType(typeof(ApiResponse<List<BeneficiaryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBeneficiaries(CancellationToken cancellationToken)
    {
        var beneficiaries = await _portal.GetBeneficiariesAsync(User.GetActor(), cancellationToken);
        return Ok(ApiResponse<List<BeneficiaryDto>>.SuccessWith(beneficiaries));
    }

    [HttpGet("payments")]
    [ProducesResponseType(typeof(ApiResponse<List<PaymentApprovalResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPayments(CancellationToken cancellationToken)
    {
        var payments = await _portal.GetPaymentsAsync(User.GetActor(), cancellationToken);
        return Ok(ApiResponse<List<PaymentApprovalResponse>>.SuccessWith(payments));
    }

    [HttpGet("payments/{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<PaymentApprovalResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPayment(int id, CancellationToken cancellationToken)
    {
        var payment = await _portal.GetPaymentAsync(id, User.GetActor(), cancellationToken);
        return Ok(ApiResponse<PaymentApprovalResponse>.SuccessWith(payment));
    }

    [HttpGet("payments/{id:int}/timeline")]
    [ProducesResponseType(typeof(ApiResponse<PaymentTimelineDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTimeline(int id, CancellationToken cancellationToken)
    {
        await RequireOwnedPaymentAsync(id, cancellationToken);
        var timeline = await _timeline.GetTimelineAsync(id, cancellationToken);
        return Ok(ApiResponse<PaymentTimelineDto>.SuccessWith(timeline));
    }

    [HttpGet("payments/{id:int}/receipt")]
    [ProducesResponseType(typeof(ApiResponse<PaymentReceiptDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetReceipt(int id, CancellationToken cancellationToken)
    {
        await RequireOwnedPaymentAsync(id, cancellationToken);
        var receipt = await _timeline.GetReceiptAsync(id, cancellationToken);
        return Ok(ApiResponse<PaymentReceiptDto>.SuccessWith(receipt));
    }

    [HttpPost("payments/{id:int}/cancel")]
    [Idempotent]
    public async Task<IActionResult> CancelPayment(int id, CancellationToken cancellationToken)
    {
        await RequireOwnedPaymentAsync(id, cancellationToken);
        await _payments.CancelAsync(id, User.GetActor(), cancellationToken);
        return NoContent();
    }

    [HttpPost("transfers")]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<PaymentApprovalResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateTransfer(
        [FromBody] CreateCustomerTransferRequest request,
        CancellationToken cancellationToken)
    {
        var payment = await _portal.CreateTransferRequestAsync(request, User.GetActor(), cancellationToken);

        return CreatedAtAction(
            nameof(GetPayment),
            new { id = payment.Id },
            ApiResponse<PaymentApprovalResponse>.SuccessWith(
                payment,
                payment.Status == "AutoApproved"
                    ? "Low risk transfer auto-approved."
                    : "Transfer request created; waiting for Admin review."));
    }

    [HttpPost("requests")]
    [Idempotent]
    [ProducesResponseType(typeof(ApiResponse<PaymentApprovalResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateOperationRequest(
        [FromBody] CreateCustomerOperationRequest request,
        CancellationToken cancellationToken)
    {
        var payment = await _portal.CreateOperationRequestAsync(request, User.GetActor(), cancellationToken);
        return CreatedAtAction(nameof(GetPayment), new { id = payment.Id },
            ApiResponse<PaymentApprovalResponse>.SuccessWith(payment, "Talep oluşturuldu; onay bekliyor."));
    }

    private async Task RequireOwnedPaymentAsync(int paymentId, CancellationToken cancellationToken)
    {
        if (!await _access.UserOwnsPaymentAsync(User.GetUserId(), paymentId, cancellationToken))
            throw new NotFoundException("Payment", paymentId.ToString());
    }
}
