using DigitalBanking.Application.DTOs.Account;
using DigitalBanking.Application.DTOs.PaymentApprovals;
using DigitalBanking.Dashboard.Services;
using Microsoft.AspNetCore.Mvc;

namespace DigitalBanking.Dashboard.Pages.Customer;

public class RequestsModel : SecurePageModel
{
    private readonly ApiClient _api;

    public RequestsModel(ApiClient api)
    {
        _api = api;
    }

    public List<AccountDto> Accounts { get; set; } = new();
    public string? Error { get; set; }
    public bool ShowSummary { get; set; }
    [BindProperty] public bool Confirm { get; set; }
    [BindProperty] public bool Edit { get; set; }

    [BindProperty] public string RequestType { get; set; } = "Deposit";
    [BindProperty] public Guid? AccountId { get; set; }
    [BindProperty] public string AccountName { get; set; } = "";
    [BindProperty] public string AccountType { get; set; } = "Checking";
    [BindProperty] public string Currency { get; set; } = "TRY";
    [BindProperty] public decimal Amount { get; set; }
    [BindProperty] public string? Description { get; set; }
    [BindProperty] public string IdempotencyKey { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(
        string? type = null,
        string? accountType = null,
        Guid? accountId = null,
        CancellationToken cancellationToken = default)
    {
        var customer = RequireCustomer();
        if (customer is not null) return customer;
        IdempotencyKey = NewIdempotencyKey();
        await LoadAsync(cancellationToken);
        if (!string.IsNullOrWhiteSpace(type)) RequestType = type;
        else if (Accounts.Count == 0) RequestType = "AccountOpening";
        if (!string.IsNullOrWhiteSpace(accountType)) AccountType = accountType;
        if (accountId is not null && Accounts.Any(x => x.Id == accountId)) AccountId = accountId;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var customer = RequireCustomer();
        if (customer is not null) return customer;
        await LoadAsync(cancellationToken);

        if (Amount < 0 || (RequestType != "AccountOpening" && Amount <= 0))
        {
            Error = "Geçerli bir tutar girin.";
            return Page();
        }
        if (RequestType != "AccountOpening" && AccountId is null)
        {
            Error = "Hesap seçin.";
            return Page();
        }
        if (RequestType == "AccountOpening" && string.IsNullOrWhiteSpace(AccountName))
        {
            Error = "Hesap adı zorunludur.";
            return Page();
        }
        if (string.IsNullOrWhiteSpace(IdempotencyKey)) IdempotencyKey = NewIdempotencyKey();

        if (Edit)
        {
            ShowSummary = false;
            return Page();
        }
        if (!Confirm)
        {
            ShowSummary = true;
            return Page();
        }

        var result = await _api.PostAsync<PaymentApprovalResponse>(Token!, "/api/me/requests", new
        {
            requestType = RequestType,
            accountId = RequestType == "AccountOpening" ? null : AccountId,
            accountName = RequestType == "AccountOpening" ? AccountName : null,
            accountType = RequestType == "AccountOpening" ? AccountType : null,
            currency = RequestType == "AccountOpening" ? Currency : null,
            amount = Amount,
            description = Description
        }, IdempotencyKey, cancellationToken);

        if (result.Unauthorized) return SessionExpired();
        if (!result.Success) { Error = result.Error; return Page(); }
        return RedirectToPage("/Customer/Payments/Details", new { id = result.Data!.Id, created = true });
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var accounts = await _api.GetAsync<List<AccountDto>>(Token!, "/api/me/accounts", cancellationToken);
        if (accounts.Success) Accounts = accounts.Data ?? new();
    }
}
