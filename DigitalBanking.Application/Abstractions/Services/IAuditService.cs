namespace DigitalBanking.Application.Abstractions.Services;

public interface IAuditService
{
    /// <summary>Adds an audit entry and saves it immediately (used for authentication events).</summary>
    Task LogAsync(
        string action,
        string entityName,
        string? entityId = null,
        string? username = null,
        bool isSuccess = true,
        string? details = null);

    /// <summary>
    /// Adds an audit entry to the current DbContext without saving. Money-moving
    /// services call this inside their database transaction so the ledger change and
    /// its audit record commit or roll back together.
    /// </summary>
    void Add(
        string action,
        string entityName,
        string? entityId = null,
        string? username = null,
        bool isSuccess = true,
        string? details = null);
}
