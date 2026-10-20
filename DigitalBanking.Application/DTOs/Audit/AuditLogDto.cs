namespace DigitalBanking.Application.DTOs.Audit;

public class AuditLogDto
{
    public Guid Id { get; set; }
    public string? Username { get; set; }
    public string Action { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public string? EntityId { get; set; }
    public bool IsSuccess { get; set; }
    public string? Details { get; set; }
    public DateTime CreatedDate { get; set; }
}
