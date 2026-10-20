using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Domain.Entities;

/// <summary>
/// Immutable business event that actually happened for a payment approval.
/// Timeline screens are derived from these rows; the UI must not invent stages.
/// </summary>
public class PaymentTimelineEvent
{
    public Guid Id { get; set; }
    public int PaymentApprovalId { get; set; }
    public Guid CorrelationId { get; set; }
    public PaymentTimelineEventType EventType { get; set; }
    public DateTime OccurredAtUtc { get; set; }
    public Guid? ActorUserId { get; set; }
    public string? ActorUsername { get; set; }
    public string? ActorRole { get; set; }
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
}
