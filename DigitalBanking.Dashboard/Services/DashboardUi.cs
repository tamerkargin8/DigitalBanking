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

    private static readonly TimeZoneInfo Istanbul =
        TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    /// <summary>UTC instants from the API are rendered in the same zone as receipts.</summary>
    public static string LocalDate(DateTime? utc)
    {
        if (utc is null) return "Süresiz";
        var value = DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(value, Istanbul).ToString("dd.MM.yyyy HH:mm", Tr);
    }

    /// <summary>Converts an HTML datetime-local value (user's local zone) to UTC.</summary>
    public static DateTime? LocalInputToUtc(DateTime? local)
    {
        if (local is null) return null;
        var unspecified = DateTime.SpecifyKind(local.Value, DateTimeKind.Unspecified);
        return TimeZoneInfo.ConvertTimeToUtc(unspecified, Istanbul);
    }

    /// <summary>Maps the few English API/business messages that can still reach a page.</summary>
    public static string SafeError(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "İşlem tamamlanamadı. Lütfen tekrar deneyin.";
        var value = message.Trim();
        var mapped = value switch
        {
            "Insufficient balance." => "Yetersiz bakiye.",
            "Insufficient balance in source account." => "Kaynak hesapta yetersiz bakiye.",
            "Source and destination accounts cannot be the same." => "Kaynak ve hedef hesap aynı olamaz.",
            "Source and destination accounts must use the same currency." => "Kaynak ve hedef hesap aynı para biriminde olmalıdır.",
            "Sender and receiver accounts cannot be the same." => "Gönderen ve alıcı hesap aynı olamaz.",
            "Sender account id is required." => "Gönderen hesap zorunludur.",
            "Receiver account id is required." => "Alıcı hesap zorunludur.",
            "Username is required." => "Kullanıcı adı zorunludur.",
            "Password is required." => "Şifre zorunludur.",
            "Description cannot exceed 250 characters." => "Açıklama 250 karakteri aşamaz.",
            "'to' date must be greater than or equal to 'from' date." => "Bitiş tarihi başlangıçtan önce olamaz.",
            "Date range cannot exceed 366 days." => "Tarih aralığı 366 günü aşamaz.",
            _ => value
        };
        if (mapped == value)
        {
            if (value.StartsWith("Amount must be greater", StringComparison.OrdinalIgnoreCase))
                mapped = "Tutar sıfırdan büyük olmalıdır.";
            else if (value.StartsWith("Amount cannot have", StringComparison.OrdinalIgnoreCase))
                mapped = "Tutar en fazla 2 ondalık basamak içerebilir.";
            else if (value.StartsWith("Amount cannot be negative", StringComparison.OrdinalIgnoreCase))
                mapped = "Tutar negatif olamaz.";
            else if (value.StartsWith("Amount exceeds", StringComparison.OrdinalIgnoreCase))
                mapped = "Tutar desteklenen üst sınırı aşıyor.";
            else if (value.Contains("must be greater than zero", StringComparison.OrdinalIgnoreCase))
                mapped = "Tutar sıfırdan büyük olmalıdır.";
            else if (value.Contains("cannot be negative", StringComparison.OrdinalIgnoreCase))
                mapped = "Tutar negatif olamaz.";
            else if (value.Contains("cannot have more than 2 decimal places", StringComparison.OrdinalIgnoreCase))
                mapped = "Tutar en fazla 2 ondalık basamak içerebilir.";
        }
        return mapped;
    }

    public static string TimelineDescription(string? value) => value switch
    {
        "Payment request recorded." => "Talep kaydedildi.",
        "Ledger movement completed." => "Finansal hareket tamamlandı.",
        "Approved by reviewer." => "Yetkili tarafından onaylandı.",
        "Rejected by reviewer." => "Yetkili tarafından reddedildi.",
        "Financial request applied." => "Finansal talep uygulandı.",
        "Request cancelled." => "Talep iptal edildi.",
        "Awaiting Owner or delegated approver." => "Owner veya yetkili onayı bekleniyor.",
        "Transfer request created." => "Transfer talebi oluşturuldu.",
        "Deposit request created." => "Yatırma talebi oluşturuldu.",
        "Withdrawal request created." => "Çekme talebi oluşturuldu.",
        "Account opening request created." => "Hesap açılışı talebi oluşturuldu.",
        _ => value ?? "—"
    };

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
        "ReconciliationException" => "Mutabakat istisnası",
        "Checking" => "Vadesiz",
        "TimeDeposit" => "Vadeli",
        "Investment" => "Yatırım",
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
