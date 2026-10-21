using DigitalBanking.Application.Exceptions;

namespace DigitalBanking.Application.Common;

/// <summary>
/// Decimal-only money validation. Amounts are never converted to double or float.
/// The ledger stores two decimal places; callers must send a value that already
/// fits that scale so amounts are rejected instead of silently rounded.
/// </summary>
public static class Money
{
    public const int Scale = 2;
    public const decimal MaximumAmount = 1_000_000_000_000m; // 1 trillion, demo guard rail

    public static void EnsurePositive(decimal amount, string fieldName = "Tutar")
    {
        if (amount <= 0)
            throw new BusinessRuleException($"{fieldName} sıfırdan büyük olmalıdır.");

        EnsureScale(amount, fieldName);
    }

    public static void EnsureNonNegative(decimal amount, string fieldName = "Tutar")
    {
        if (amount < 0)
            throw new BusinessRuleException($"{fieldName} negatif olamaz.");

        EnsureScale(amount, fieldName);
    }

    public static void EnsureScale(decimal amount, string fieldName = "Tutar")
    {
        if (amount > MaximumAmount)
            throw new BusinessRuleException($"{fieldName} desteklenen üst sınırı aşıyor.");

        if (decimal.Round(amount, Scale) != amount)
            throw new BusinessRuleException($"{fieldName} en fazla {Scale} ondalık basamak içerebilir.");
    }
}
