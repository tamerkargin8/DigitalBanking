namespace DigitalBanking.Application.DTOs.PaymentApprovals;

public class CreatePaymentApprovalRequest
{
    public Guid FromAccountId { get; set; }
    public Guid ToAccountId { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
