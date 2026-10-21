namespace DigitalBanking.Application.DTOs.ApprovalRequests;

public class CreateCustomerOperationRequest
{
    public string RequestType { get; set; } = string.Empty;
    public Guid? AccountId { get; set; }
    public string? AccountName { get; set; }
    public string? AccountType { get; set; }
    public string? Currency { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
}
