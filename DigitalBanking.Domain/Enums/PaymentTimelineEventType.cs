namespace DigitalBanking.Domain.Enums;

public enum PaymentTimelineEventType
{
    Created = 1,
    Pending = 2,
    AutoApproved = 3,
    Approved = 4,
    Rejected = 5,
    Completed = 6,
    Cancelled = 7
}
