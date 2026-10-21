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
    public string SelectedType { get; set; } = "All";
    public string? Error { get; set; }

    public IReadOnlyList<(string Key, string Label, int Count)> Tabs => new[]
    {
        ("All", "Tümü", Accounts.Count),
        ("Checking", "Vadesiz", Accounts.Count(x => x.AccountType == "Checking")),
        ("TimeDeposit", "Vadeli", Accounts.Count(x => x.AccountType == "TimeDeposit")),
        ("Investment", "Yatırım", Accounts.Count(x => x.AccountType == "Investment"))
    };

    public List<AccountDto> FilteredAccounts => SelectedType == "All"
        ? Accounts
        : Accounts.Where(x => string.Equals(x.AccountType, SelectedType, StringComparison.OrdinalIgnoreCase)).ToList();

    public async Task<IActionResult> OnGetAsync(string? type = null, CancellationToken cancellationToken = default)
    {
        var customer = RequireCustomer();
        if (customer is not null) return customer;

        SelectedType = type is "Checking" or "TimeDeposit" or "Investment" ? type : "All";
        var result = await _api.GetAsync<List<AccountDto>>(Token!, "/api/me/accounts", cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        Accounts = result.Data ?? new();
        return Page();
    }
}
