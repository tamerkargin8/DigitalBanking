namespace DigitalBanking.Infrastructure.Services;

/// <summary>Configurable demo SLA for pending approvals. Not a regulatory SLA.</summary>
public sealed class OperationsOptions
{
    public const string SectionName = "Operations";

    /// <summary>Minutes after which a pending approval is shown as overdue (inclusive).</summary>
    public int PendingSlaMinutes { get; set; } = 30;
}
