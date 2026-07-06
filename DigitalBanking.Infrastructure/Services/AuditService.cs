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

    public async Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string? userId,
        string? username = null,
        string? details = null)
    {
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Username = username,
            Details = details,
            CreatedDate = DateTime.UtcNow
        };

        await _context.AuditLogs.AddAsync(auditLog);
        await _context.SaveChangesAsync();
    }
}