using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Net.Http.Json;

namespace DigitalBanking.Dashboard.Pages.Reports;

public class TopAccountsModel : PageModel
{
    private readonly ApiClient _api;

    public TopAccountsModel(ApiClient api)
    {
        _api = api;
    }

    public List<TopAccountItem> Items { get; set; } = new();
    public string? Error { get; set; }

    // UI state
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public int Limit { get; set; } = 10;

    public async Task<IActionResult> OnGetAsync(string? from = null, string? to = null, int limit = 10)
    {
        var token = HttpContext.Session.GetString("jwt");
        if (string.IsNullOrEmpty(token))
            return RedirectToPage("/Login");

        From = string.IsNullOrWhiteSpace(from)
            ? DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd")
            : from;

        To = string.IsNullOrWhiteSpace(to)
            ? DateTime.UtcNow.ToString("yyyy-MM-dd")
            : to;

        Limit = (limit <= 0) ? 10 : limit;

        try
        {
            var http = _api.Create(token);

            // Swagger'da gördüğün endpoint:
            var url = $"/api/Transactions/top-accounts?from={From}&to={To}&limit={Limit}";

            var data = await http.GetFromJsonAsync<List<TopAccountItem>>(url);
            Items = data ?? new List<TopAccountItem>();

            // Eğer API NetFlow dönmüyorsa client-side hesapla
            foreach (var x in Items)
            {
                if (x.NetFlow == 0)
                    x.NetFlow = x.DepositTotal - x.WithdrawTotal;
            }
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }

        return Page();
    }

    public class TopAccountItem
    {
        public Guid AccountId { get; set; }
        public int TransactionCount { get; set; }
        public decimal DepositTotal { get; set; }
        public decimal WithdrawTotal { get; set; }
        public decimal NetFlow { get; set; }
    }
}