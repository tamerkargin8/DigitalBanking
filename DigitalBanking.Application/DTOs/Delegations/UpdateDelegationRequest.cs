namespace DigitalBanking.Application.DTOs.Delegations;

public class UpdateDelegationRequest
{
    public string? Operation { get; set; }
    public string? Currency { get; set; }
    public decimal? MaxAmount { get; set; }
    public decimal? DailyLimit { get; set; }
    public bool? IsActive { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
