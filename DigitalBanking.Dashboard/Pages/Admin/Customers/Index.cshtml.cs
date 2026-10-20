using DigitalBanking.Application.DTOs.Customers;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Customers;

public class IndexModel : SecurePageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public List<CustomerResponse> Items { get; set; } = new();
    public string? Error { get; set; }
    public string? Flash { get; set; }
    [BindProperty] public string FirstName { get; set; } = "";
    [BindProperty] public string LastName { get; set; } = "";
    [BindProperty] public string IdentityNumber { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(string? flash = null, CancellationToken cancellationToken = default)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        Flash = flash;
        var result = await _api.GetAsync<List<CustomerResponse>>(Token!, "/api/customers", cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        Items = result.Data ?? new();
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        var result = await _api.PostAsync<CustomerResponse>(Token!, "/api/customers", new
        {
            firstName = FirstName,
            lastName = LastName,
            identityNumber = IdentityNumber
        }, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        return RedirectToPage(new { flash = result.Success ? "Müşteri oluşturuldu." : result.Error });
    }
}
