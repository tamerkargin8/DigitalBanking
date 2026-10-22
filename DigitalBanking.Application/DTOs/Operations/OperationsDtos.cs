namespace DigitalBanking.Application.DTOs.Operations;

/// <summary>Read-only operations control-centre projections (UTC + Istanbul display).</summary>
public class OperationsSummaryDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public DateTime GeneratedAtIstanbul { get; set; }
    public int PendingSlaMinutes { get; set; }

    public int PendingCount { get; set; }
    public int OverdueCount { get; set; }
    public int UnassignedCount { get; set; }
    public int AssignedToMeCount { get; set; }
    public int OldestPendingAgeMinutes { get; set; }

    public bool CanViewReconciliation { get; set; }
    public int UnresolvedReconciliationBatchCount { get; set; }
    public int OpenReconciliationExceptionCount { get; set; }
}

public class OperationsAssigneeDto
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsCurrentUser { get; set; }
}

public class OperationsApprovalRowDto
{
    public int Id { get; set; }
    public string RequestType { get; set; } = string.Empty;
    public string RequestTypeLabel { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public Guid RequestedByUserId { get; set; }
    public string? RequestedByUsername { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AgeMinutes { get; set; }
    public int SlaMinutes { get; set; }
    public bool IsOverdue { get; set; }

    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUsername { get; set; }
    public DateTime? AssignedAt { get; set; }
    public bool IsAssignedToMe { get; set; }

    public bool CanAssign { get; set; }
    public List<OperationsAssigneeDto> Assignees { get; set; } = new();
}

public class OperationsReconciliationItemDto
{
    public Guid BatchId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? CurrencyScope { get; set; }
    public DateTime CreatedAt { get; set; }
    public int AgeMinutes { get; set; }
    public int RowCount { get; set; }
    public int DifferenceCount { get; set; }
    public int OnlyExternalCount { get; set; }
    public int OnlySystemCount { get; set; }
    public int AmountMismatchCount { get; set; }
    public int CurrencyMismatchCount { get; set; }
    public int DuplicateReferenceCount { get; set; }
    public int ExceptionCount { get; set; }
    public int UnresolvedExceptionCount { get; set; }
    /// <summary>"NoException" or "Open".</summary>
    public string ResolutionState { get; set; } = string.Empty;
}

public class OperationsAssignmentResultDto
{
    public int ApprovalId { get; set; }
    public Guid? AssignedToUserId { get; set; }
    public string? AssignedToUsername { get; set; }
    public bool Changed { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AssignApprovalRequest
{
    /// <summary>Null clears the current assignment.</summary>
    public Guid? AssignedToUserId { get; set; }
}
