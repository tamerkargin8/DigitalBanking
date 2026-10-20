namespace DigitalBanking.Application.DTOs.CustomerPortal;

/// <summary>Recipient directory entry. No balance and no full account number.</summary>
public class BeneficiaryDto
{
    public Guid AccountId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string MaskedAccountNumber { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
}
