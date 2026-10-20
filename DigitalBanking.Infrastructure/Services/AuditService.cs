using DigitalBanking.Application.Abstractions.Services;
using DigitalBanking.Domain.Entities;
using DigitalBanking.Infrastructure.Persistence;

namespace DigitalBanking.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly BankDbContext _context;

    public AuditService(BankDbContext context)
    {
        _context = context;
    }

    public void Add(
        string action,
        string entityName,
        string? entityId = null,
        string? username = null,
        bool isSuccess = true,
        string? details = null)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Username = username,
            IsSuccess = isSuccess,
            Details = details,
            CreatedDate = DateTime.UtcNow
        });
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string? entityId = null,
        string? username = null,
        bool isSuccess = true,
        string? details = null)
    {
        Add(action, entityName, entityId, username, isSuccess, details);
        await _context.SaveChangesAsync();
    }
}
