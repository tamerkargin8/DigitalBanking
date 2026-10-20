using System.Globalization;
using DigitalBanking.Application.Common;

namespace DigitalBanking.Dashboard.Services;

/// <summary>Turkish labels and currency-by-currency formatting for the Razor UI.</summary>
public static class DashboardUi
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    public static string Money(decimal amount, string currency) =>
        $"{amount.ToString("N2", Tr)} {currency}";

    public static string Date(DateTime value) => value.ToString("dd.MM.yyyy HH:mm", Tr);

    public static string DateOnly(DateTime value) => value.ToString("dd.MM.yyyy", Tr);

    public static string Account(string? accountNumber) => Masking.AccountNumber(accountNumber);

    public static string ShortId(Guid id) => id.ToString("N")[..8];

    public static string Status(string? value) => value switch
    {
        "Pending" => "Bekliyor",
        "AutoApproved" => "Otomatik onay",
        "Approved" => "Onaylandı",
        "Rejected" => "Reddedildi",
        "Cancelled" => "İptal",
        "Open" => "Açık",
        "InReview" => "İncelemede",
        "ResolutionProposed" => "Çözüm önerildi",
        "Closed" => "Kapandı",
        "Previewed" => "Önizlendi",
        "Invalid" => "Hatalı",
        "Imported" => "İçe aktarıldı",
        "Importing" => "İçe aktarılıyor",
        "Failed" => "Başarısız",
        "Matched" => "Eşleşti",
        "OnlyExternal" => "Yalnız dış dosyada",
        "OnlySystem" => "Yalnız sistemde",
        "AmountMismatch" => "Tutar uyuşmazlığı",
        "CurrencyMismatch" => "Para birimi uyuşmazlığı",
        "DuplicateReference" => "Mükerrer referans",
        "Unmatched" => "Eşleşmedi",
        "Low" => "Düşük",
        "Medium" => "Orta",
        "High" => "Yüksek",
        "Critical" => "Kritik",
        "Debit" => "Borç",
        "Credit" => "Alacak",
        "Transfer" => "Transfer",
        "Deposit" => "Yatırma",
        "Withdrawal" => "Çekme",
        "AccountOpening" => "Hesap açılışı",
        "PendingActivation" => "Aktivasyon bekliyor",
        "Active" => "Aktif",
        "Disabled" => "Devre dışı",
        "Owner" => "Owner",
        "DelegatedApprover" => "Yetkili",
        "Customer" => "Müşteri",
        _ => value ?? "—"
    };

    public static string StatusBadgeClass(string? value) => value switch
    {
        "Pending" or "InReview" or "ResolutionProposed" or "Previewed" or "PendingActivation" => "badge status-pending",
        "Approved" or "AutoApproved" or "Matched" or "Closed" or "Imported" or "Active" => "badge status-ok",
        "Rejected" or "Invalid" or "AmountMismatch" or "CurrencyMismatch" or "DuplicateReference" or "Failed" or "Disabled" => "badge status-bad",
        "OnlyExternal" or "OnlySystem" or "Open" => "badge status-warn",
        _ => "badge bg-secondary"
    };

    public static string RiskBadgeClass(string? value) => value switch
    {
        "Low" => "badge risk-low",
        "Medium" => "badge risk-medium",
        "High" => "badge risk-high",
        "Critical" => "badge risk-critical",
        _ => "badge bg-secondary"
    };

    public static string TransactionType(string? value) => value switch
    {
        "Deposit" => "Yatırma",
        "Withdraw" => "Çekme",
        "TransferIn" => "Transfer giriş",
        "TransferOut" => "Transfer çıkış",
        _ => value ?? "—"
    };

    public static string TimelineType(string? value) => value switch
    {
        "Created" => "Oluşturuldu",
        "Pending" => "Onay bekliyor",
        "AutoApproved" => "Otomatik onaylandı",
        "Approved" => "Onaylandı",
        "Rejected" => "Reddedildi",
        "Completed" => "Tamamlandı",
        "Cancelled" => "İptal edildi",
        _ => value ?? "—"
    };
}
