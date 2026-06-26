namespace DigitalBanking.Domain.Entities;

public class Account
{
    public Guid Id { get; set; }
    public string AccountNumber { get; set; } = null!;
    public decimal Balance { get; set; } = 0;
    public string Currency { get; set; } = "TRY";

    public Guid CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

}
