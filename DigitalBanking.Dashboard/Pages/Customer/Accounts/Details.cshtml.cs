using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.Transaction;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Customer.Accounts;

public class DetailsModel : SecurePageModel
{
    private readonly ApiClient _api;

    public DetailsModel(ApiClient api)
    {
        _api = api;
    }

    public AccountDetailDto? Account { get; set; }
    public TransactionHistoryDto? History { get; set; }
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is not null) return customer;

        var account = await _api.GetAsync<AccountDetailDto>(Token!, $"/api/me/accounts/{id}", cancellationToken);
        if (account.Unauthorized) return SessionExpired();
        if (!account.Success) { Error = account.Error; return Page(); }
        Account = account.Data;

        var history = await _api.GetAsync<TransactionHistoryDto>(Token!, $"/api/me/accounts/{id}/transactions?pageSize=20", cancellationToken);
        if (history.Success) History = history.Data;
        return Page();
    }
}
