namespace DigitalBanking.Application.DTOs.Account;

/// <summary>
/// Basic account information DTO.
/// </summary>
public class AccountDto
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = "Vadesiz Hesap";
    public string AccountType { get; set; } = "Checking";
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "TRY";
    public Guid CustomerId { get; set; }
    public DateTime CreatedDate { get; set; }
}
