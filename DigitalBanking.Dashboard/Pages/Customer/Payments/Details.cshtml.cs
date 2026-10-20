using DigitalBanking.Application.DTOs.CustomerPortal;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Customer.Payments;

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
    public string? Flash { get; set; }
    public bool Created { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, bool created = false, string? flash = null, CancellationToken cancellationToken = default)
    {
        var customer = RequireCustomer();
        if (customer is not null) return customer;
        Created = created;
        Flash = flash;

        var payment = await _api.GetAsync<PaymentApprovalResponse>(Token!, $"/api/me/payments/{id}", cancellationToken);
        if (payment.Unauthorized) return SessionExpired();
        if (!payment.Success) { Error = payment.Error; return Page(); }
        Payment = payment.Data;

        var timeline = await _api.GetAsync<PaymentTimelineDto>(Token!, $"/api/me/payments/{id}/timeline", cancellationToken);
        if (timeline.Success) Timeline = timeline.Data;
        return Page();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is not null) return customer;
        var result = await _api.PostAsync<object>(Token!, $"/api/me/payments/{id}/cancel", null, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new { id, flash = result.Success ? "Talep iptal edildi; bakiye değişmedi." : result.Error });
    }
}
