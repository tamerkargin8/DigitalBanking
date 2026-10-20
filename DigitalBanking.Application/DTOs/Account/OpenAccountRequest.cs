namespace DigitalBanking.Application.DTOs.Account;

public class OpenAccountRequest
{
    public Guid CustomerId { get; set; }

    /// <summary>ISO-style currency code; defaults to TRY.</summary>
    public string Currency { get; set; } = "TRY";

    /// <summary>
    /// Optional opening deposit. Accepted only from an Admin and recorded as a
    /// Deposit ledger transaction so the opening balance is auditable.
    /// </summary>
    public decimal InitialBalance { get; set; }
}
