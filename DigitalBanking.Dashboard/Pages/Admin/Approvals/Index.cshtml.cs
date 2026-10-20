using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Approvals;

public class IndexModel : SecurePageModel
{
    private readonly ApiClient _api;

    public IndexModel(ApiClient api)
    {
        _api = api;
    }

    public List<PaymentApprovalResponse> Items { get; set; } = new();
    public string? Error { get; set; }
    public string Status { get; set; } = "Pending";
    public string? RequestType { get; set; }
    public string? Currency { get; set; }
    public string? Scope { get; set; }

    public async Task<IActionResult> OnGetAsync(
        string? status = "Pending", string? requestType = null,
        string? currency = null, string? scope = null, CancellationToken cancellationToken = default)
    {
        var approver = RequireApprover();
        if (approver is not null) return approver;

        Status = string.IsNullOrWhiteSpace(status) ? "Pending" : status;
        RequestType = requestType;
        Currency = currency;
        Scope = scope;

        var query = $"/api/paymentapprovals/queue?status={Uri.EscapeDataString(Status)}";
        if (!string.IsNullOrWhiteSpace(RequestType)) query += $"&requestType={Uri.EscapeDataString(RequestType)}";
        if (!string.IsNullOrWhiteSpace(Currency)) query += $"&currency={Uri.EscapeDataString(Currency)}";
        if (!string.IsNullOrWhiteSpace(Scope)) query += $"&scope={Uri.EscapeDataString(Scope)}";

        var result = await _api.GetAsync<List<PaymentApprovalResponse>>(Token!, query, cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success)
        {
            Error = result.Error;
            return Page();
        }

        Items = result.Data ?? new();
        return Page();
    }
}
