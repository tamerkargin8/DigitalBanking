using DigitalBanking.Application.Exceptions;

namespace DigitalBanking.Application.Common;

/// <summary>
/// Currency values supported by the demo. No live FX conversion is performed, so
/// transfers and payment approvals require both accounts to use the same currency.
/// </summary>
public static class CurrencyCodes
{
    public const string Default = "TRY";

    private static readonly HashSet<string> Supported = new(StringComparer.OrdinalIgnoreCase)
    {
        "TRY", "USD", "EUR", "GBP"
    };

    public static string Normalize(string? currency)
    {
        var normalized = string.IsNullOrWhiteSpace(currency)
            ? Default
            : currency.Trim().ToUpperInvariant();

        if (!Supported.Contains(normalized))
            throw new BusinessRuleException($"Unsupported currency '{normalized}'. Supported values: TRY, USD, EUR, GBP.");

        return normalized;
    }
}
