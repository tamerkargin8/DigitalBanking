using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.CustomerPortal;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Customer;

public class HomeModel : SecurePageModel
{
    private readonly ApiClient _api;

    public HomeModel(ApiClient api)
    {
        _api = api;
    }

    public CustomerProfileDto? Profile { get; set; }
    public List<AccountDto> Accounts { get; set; } = new();
    public List<PaymentApprovalResponse> Payments { get; set; } = new();
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is not null) return customer;

        var profile = await _api.GetAsync<CustomerProfileDto>(Token!, "/api/me/profile", cancellationToken);
        if (profile.Unauthorized) return SessionExpired();
        if (!profile.Success) { Error = profile.Error; return Page(); }
        Profile = profile.Data;

        var accounts = await _api.GetAsync<List<AccountDto>>(Token!, "/api/me/accounts", cancellationToken);
        if (accounts.Unauthorized) return SessionExpired();
        if (!accounts.Success) { Error = accounts.Error; return Page(); }
        Accounts = accounts.Data ?? new();

        var payments = await _api.GetAsync<List<PaymentApprovalResponse>>(Token!, "/api/me/payments", cancellationToken);
        if (payments.Success) Payments = payments.Data ?? new();
        return Page();
    }

    public IEnumerable<IGrouping<string, AccountDto>> AccountsByCurrency =>
        Accounts.GroupBy(x => x.Currency).OrderBy(x => x.Key);
}
