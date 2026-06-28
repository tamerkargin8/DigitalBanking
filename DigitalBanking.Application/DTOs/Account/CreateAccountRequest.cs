namespace DigitalBanking.Application.DTOs.Account;

public class CreateAccountRequest
{
    public Guid CustomerId { get; set; }
    public decimal InitialBalance { get; set; }
    public string Currency { get; set; } = "TRY";
}
