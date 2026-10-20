namespace DigitalBanking.Application.DTOs.CustomerPortal;

public class CustomerProfileDto
{
    public Guid CustomerId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string MaskedIdentityNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
