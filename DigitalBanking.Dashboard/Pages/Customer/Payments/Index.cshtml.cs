using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Customer.Payments;

public class IndexModel : SecurePageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public List<PaymentApprovalResponse> Items { get; set; } = new();
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is not null) return customer;
        var result = await _api.GetAsync<List<PaymentApprovalResponse>>(Token!, "/api/me/payments", cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        Items = result.Data ?? new();
        return Page();
    }
}
