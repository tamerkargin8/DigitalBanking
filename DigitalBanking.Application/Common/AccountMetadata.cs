using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Application.Common;

/// <summary>Validation and safe defaults for customer-editable account metadata.</summary>
public static class AccountMetadata
{
    public const int MaxNameLength = 100;

    public static string DefaultName(AccountType type) => type switch
    {
        AccountType.Checking => "Vadesiz Hesap",
        AccountType.TimeDeposit => "Vadeli Hesap",
        AccountType.Investment => "Yatırım Hesabı",
        _ => "Vadesiz Hesap"
    };

    public static AccountType ParseType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return AccountType.Checking;
        if (!Enum.TryParse<AccountType>(value.Trim(), ignoreCase: true, out var type)
            || !Enum.IsDefined(type))
            throw new BusinessRuleException("Hesap türü Vadesiz, Vadeli veya Yatırım olmalıdır.");
        return type;
    }

    public static string NormalizeName(string? value, AccountType type)
    {
        if (string.IsNullOrWhiteSpace(value)) return DefaultName(type);
        return RequireName(value);
    }

    public static string RequireName(string? value)
    {
        var name = (value ?? string.Empty).Trim();
        if (name.Length == 0)
            throw new BusinessRuleException("Hesap adı zorunludur.");
        if (name.Length > MaxNameLength)
            throw new BusinessRuleException($"Hesap adı en fazla {MaxNameLength} karakter olabilir.");
        if (name.Any(char.IsControl) || name.Contains('<') || name.Contains('>'))
            throw new BusinessRuleException("Hesap adı kontrol karakterleri veya HTML işaretleri içeremez.");
        return name;
    }
}
