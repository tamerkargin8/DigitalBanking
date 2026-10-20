namespace DigitalBanking.Application.Common;

/// <summary>
/// Presentation helpers. Full account numbers and identity numbers must not be
/// rendered in customer screens or receipts.
/// </summary>
public static class Masking
{
    public static string AccountNumber(string? accountNumber)
    {
        if (string.IsNullOrWhiteSpace(accountNumber)) return "••••";
        var compact = accountNumber.Replace(" ", string.Empty).Replace("-", string.Empty);
        var last4 = compact.Length <= 4 ? compact : compact[^4..];
        return $"•••• {last4}";
    }

    public static string IdentityNumber(string? identityNumber)
    {
        if (string.IsNullOrWhiteSpace(identityNumber)) return "••••";
        var compact = identityNumber.Trim();
        var last4 = compact.Length <= 4 ? compact : compact[^4..];
        return $"••••••••{last4}";
    }

    public static string PaymentReference(int paymentId, Guid correlationId)
    {
        var shortId = correlationId.ToString("N")[..8].ToUpperInvariant();
        return $"PAY-{paymentId:D6}-{shortId}";
    }
}
