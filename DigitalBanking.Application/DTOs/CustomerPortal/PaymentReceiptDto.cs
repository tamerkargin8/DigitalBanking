namespace DigitalBanking.Application.DTOs.CustomerPortal;

public class PaymentReceiptDto
{
    public int PaymentId { get; set; }
    public bool IsCompleted { get; set; }
    public string Reference { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StatusSummary { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public DateTime? CompletedAtUtc { get; set; }
    public DateTime? CompletedAtLocal { get; set; }
    public string TimeZoneId { get; set; } = "Europe/Istanbul";
    public string? FromAccountMasked { get; set; }
    public string? ToAccountMasked { get; set; }
    public string? Description { get; set; }
    public string Disclaimer { get; set; } = "DEMO — GERÇEK BANKACILIK DEKONTU DEĞİLDİR";
}
