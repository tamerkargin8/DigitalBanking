using DigitalBanking.Application.DTOs.Transaction;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Reports;

public class TopAccountsModel : SecurePageModel
{
    private readonly ApiClient _api;

    public TopAccountsModel(ApiClient api)
    {
        _api = api;
    }

    public List<TopAccountTransactionResponse> Items { get; set; } = new();
    public string? Error { get; set; }

    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Currency { get; set; } = "TRY";
    public int Limit { get; set; } = 10;

    public async Task<IActionResult> OnGetAsync(
        string? from = null,
        string? to = null,
        string? currency = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var login = RequireLogin();
        if (login is not null) return login;

        Currency = string.IsNullOrWhiteSpace(currency) ? "TRY" : currency.Trim().ToUpperInvariant();
        if (Currency is not ("TRY" or "USD" or "EUR" or "GBP"))
        {
            Error = "Desteklenmeyen para birimi. TRY, USD, EUR veya GBP seçin.";
            From = from ?? "";
            To = to ?? "";
            return Page();
        }

        if (!ReportFilter.TryParse(from, to, limit.ToString(), out var fromDate, out var toDate, out var take, out var filterError))
        {
            Error = filterError;
            From = from ?? "";
            To = to ?? "";
            return Page();
        }

        From = ReportFilter.ToInputValue(fromDate);
        To = ReportFilter.ToInputValue(toDate);
        Limit = take;

        var url = "/api/Transactions/top-accounts" +
                  $"?from={Uri.EscapeDataString(From)}" +
                  $"&to={Uri.EscapeDataString(To)}" +
                  $"&take={Limit}" +
                  $"&currency={Uri.EscapeDataString(Currency)}";

        var result = await _api.GetAsync<List<TopAccountTransactionResponse>>(Token!, url, cancellationToken);
        if (result.Unauthorized) return SessionExpired();

        if (!result.Success)
        {
            Error = result.Error;
            return Page();
        }

        Items = result.Data ?? new List<TopAccountTransactionResponse>();
        return Page();
    }
}
