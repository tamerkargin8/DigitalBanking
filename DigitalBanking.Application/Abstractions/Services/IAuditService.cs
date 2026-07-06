using System.Threading.Tasks;

namespace DigitalBanking.Application.Abstractions.Services;

public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityName,
        string? entityId = null,
        string? userId = null,
        string? username = null,
        string? description = null);
}