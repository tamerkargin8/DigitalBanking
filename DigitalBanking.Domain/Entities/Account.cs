using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Domain.Entities;

public class Account
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = "Vadesiz Hesap";
    public AccountType AccountType { get; set; } = AccountType.Checking;
    public decimal Balance { get; set; }
    public string Currency { get; set; } = "TRY";
    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
    public bool IsDemo { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
