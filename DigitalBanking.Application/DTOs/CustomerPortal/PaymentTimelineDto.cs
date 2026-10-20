namespace DigitalBanking.Application.DTOs.CustomerPortal;

public class PaymentTimelineDto
{
    public int PaymentId { get; set; }
    public string Reference { get; set; } = string.Empty;
    public Guid CorrelationId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public string TimeZoneId { get; set; } = "Europe/Istanbul";
    public List<PaymentTimelineEventDto> Events { get; set; } = new();
}

public class PaymentTimelineEventDto
{
    public string Type { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime OccurredAtLocal { get; set; }
    public string? ActorUsername { get; set; }
    public string? ActorRole { get; set; }
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
}
