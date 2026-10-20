namespace DigitalBanking.Application.DTOs.Delegations;

public class CreateDelegationRequest
{
    public Guid UserId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string? Currency { get; set; }
    public decimal? MaxAmount { get; set; }
    public decimal? DailyLimit { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
