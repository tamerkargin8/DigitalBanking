using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.Common;
using DigitalBanking.Application.DTOs.Customers;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Admin.Requests;

public class NewModel : SecurePageModel
{
    private readonly ApiClient _api;

    public NewModel(ApiClient api)
    {
        _api = api;
    }

    public List<CustomerResponse> Customers { get; set; } = new();
    public List<AccountDto> Accounts { get; set; } = new();
    public string? Error { get; set; }
    public string? Flash { get; set; }

    [BindProperty] public string RequestType { get; set; } = "Transfer";
    [BindProperty] public Guid? CustomerId { get; set; }
    [BindProperty] public Guid? AccountId { get; set; }
    [BindProperty] public Guid? ToAccountId { get; set; }
    [BindProperty] public string Currency { get; set; } = "TRY";
    [BindProperty] public decimal Amount { get; set; }
    [BindProperty] public string? Description { get; set; }

    public async Task<IActionResult> OnGetAsync(string? type = null, Guid? accountId = null, CancellationToken cancellationToken = default)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;
        if (!string.IsNullOrWhiteSpace(type)) RequestType = type;
        AccountId = accountId;
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var admin = RequireAdmin();
        if (admin is not null) return admin;

        var result = await _api.PostAsync<PaymentApprovalResponse>(Token!, "/api/paymentapprovals/requests", new
        {
            requestType = RequestType,
            customerId = RequestType == "AccountOpening" ? CustomerId : null,
            accountId = AccountId,
            toAccountId = RequestType == "Transfer" ? ToAccountId : null,
            currency = RequestType == "AccountOpening" ? Currency : null,
            amount = Amount,
            description = Description
        }, NewIdempotencyKey(), cancellationToken);
        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; await LoadAsync(cancellationToken); return Page(); }
        return RedirectToPage("/Admin/Approvals/Details", new { id = result.Data!.Id });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var customers = await _api.GetAsync<List<CustomerResponse>>(Token!, "/api/customers", cancellationToken);
        if (customers.Success) Customers = customers.Data ?? new();
        var accounts = await _api.GetAsync<PaginatedResponse<AccountDto>>(Token!, "/api/accounts?pageSize=100", cancellationToken);
        if (accounts.Success) Accounts = accounts.Data?.Items ?? new();
    }
}
