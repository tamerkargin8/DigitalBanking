using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Customer.Accounts;

public class IndexModel : SecurePageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public List<AccountDto> Accounts { get; set; } = new();
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is not null) return customer;
        var result = await _api.GetAsync<List<AccountDto>>(Token!, "/api/me/accounts", cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        Accounts = result.Data ?? new();
        return Page();
    }
}
