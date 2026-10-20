using DigitalBanking.Application.DTOs.CustomerPortal;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Customer.Payments;

public class ReceiptModel : SecurePageModel
{
    private readonly ApiClient _api;

    public ReceiptModel(ApiClient api)
    {
        _api = api;
    }

    public PaymentReceiptDto? Receipt { get; set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(int id, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is not null) return customer;

        var result = await _api.GetAsync<PaymentReceiptDto>(Token!, $"/api/me/payments/{id}/receipt", cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        Receipt = result.Data;
        return Page();
    }
}
