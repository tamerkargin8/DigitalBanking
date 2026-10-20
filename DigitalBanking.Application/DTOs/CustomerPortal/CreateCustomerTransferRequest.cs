namespace DigitalBanking.Application.DTOs.CustomerPortal;

public class CreateCustomerTransferRequest
{
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
