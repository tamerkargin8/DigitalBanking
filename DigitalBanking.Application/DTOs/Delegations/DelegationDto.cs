namespace DigitalBanking.Application.DTOs.Delegations;

public class DelegationDto
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Operation { get; set; } = string.Empty;
    public string? Currency { get; set; }
    public decimal? MaxAmount { get; set; }
    public decimal? DailyLimit { get; set; }
    public bool IsActive { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string CreatedByOwnerUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
