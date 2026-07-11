namespace DigitalBanking.Application.Abstractions.Services;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityName,
        string? entityId = null,
        string? username = null,
        bool isSuccess = true,
        string? details = null);
}