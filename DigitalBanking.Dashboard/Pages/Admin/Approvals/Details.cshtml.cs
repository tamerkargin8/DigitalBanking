using DigitalBanking.Application.DTOs.CustomerPortal;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Approvals;

public class DetailsModel : SecurePageModel
{
    private readonly ApiClient _api;

    public DetailsModel(ApiClient api)
    {
        _api = api;
    }

    public PaymentApprovalResponse? Payment { get; set; }
    public PaymentTimelineDto? Timeline { get; set; }
    public string? Error { get; set; }
    [TempData] public string? Flash { get; set; }
    [BindProperty] public string? RejectionReason { get; set; }
    [BindProperty] public int Id { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var approver = RequireApprover();
        if (approver is not null) return approver;
        Id = id;

        var payment = await _api.GetAsync<PaymentApprovalResponse>(Token!, $"/api/paymentapprovals/{id}", cancellationToken);
        if (payment.Unauthorized) return SessionExpired();
        if (!payment.Success)
        {
            Error = payment.Error;
            return Page();
        }
        Payment = payment.Data;

        var timeline = await _api.GetAsync<PaymentTimelineDto>(Token!, $"/api/paymentapprovals/{id}/timeline", cancellationToken);
        if (timeline.Success) Timeline = timeline.Data;
        return Page();
    }

    public async Task<IActionResult> OnPostApproveAsync(int id, CancellationToken cancellationToken)
    {
        var approver = RequireApprover();
        if (approver is not null) return approver;

        var result = await _api.PostAsync<object>(Token!, $"/api/paymentapprovals/{id}/approve", null,
            NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        Flash = result.Success ? "Talep onaylandı ve finansal hareket uygulandı." : result.Error;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostRejectAsync(int id, CancellationToken cancellationToken)
    {
        var approver = RequireApprover();
        if (approver is not null) return approver;

        if (string.IsNullOrWhiteSpace(RejectionReason) || RejectionReason.Trim().Length < 3)
        {
            Flash = "Red gerekçesi en az 3 karakter olmalıdır.";
            return RedirectToPage(new { id });
        }

        var result = await _api.PostAsync<object>(Token!, $"/api/paymentapprovals/{id}/reject",
            new { rejectionReason = RejectionReason.Trim() }, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        Flash = result.Success ? "Talep reddedildi; bakiye değişmedi." : result.Error;
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostCancelAsync(int id, CancellationToken cancellationToken)
    {
        var approver = RequireApprover();
        if (approver is not null) return approver;

        var result = await _api.PostAsync<object>(Token!, $"/api/paymentapprovals/{id}/cancel", null,
            NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        Flash = result.Success ? "Talep iptal edildi; bakiye değişmedi." : result.Error;
        return RedirectToPage(new { id });
    }
}
