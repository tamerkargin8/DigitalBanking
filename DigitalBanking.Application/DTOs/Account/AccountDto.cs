namespace DigitalBanking.Application.DTOs.Account;

/// <summary>
/// Basic account information DTO.
/// </summary>
public class AccountDto
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public decimal Balance { get; set; }
    public Guid CustomerId { get; set; }
    public DateTime CreatedDate { get; set; }
}
