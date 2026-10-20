using DigitalBanking.Application.Common;
using DigitalBanking.Application.Exceptions;
using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Tests;

public class MoneyAndPolicyTests
{
    [Theory]
    [InlineData("0.01")]
    [InlineData("10.10")]
    [InlineData("999999.99")]
    public void ValidPositiveAmountsAreAccepted(string value)
    {
        Money.EnsurePositive(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.001")]
    [InlineData("0.005")]
    public void InvalidPositiveAmountsAreRejected(string value)
    {
        var amount = decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<BusinessRuleException>(() => Money.EnsurePositive(amount));
    }

    [Fact]
    public void NegativeOpeningBalanceIsRejectedButZeroIsAllowed()
    {
        Assert.Throws<BusinessRuleException>(() => Money.EnsureNonNegative(-0.01m));
        Money.EnsureNonNegative(0m);
    }

    [Theory]
    [InlineData("try", "TRY")]
    [InlineData("usd", "USD")]
    [InlineData(null, "TRY")]
    [InlineData("EUR", "EUR")]
    public void SupportedCurrenciesAreNormalized(string? input, string expected)
    {
        Assert.Equal(expected, CurrencyCodes.Normalize(input));
    }

    [Fact]
    public void UnsupportedCurrencyIsRejected()
    {
        Assert.Throws<BusinessRuleException>(() => CurrencyCodes.Normalize("BTC"));
    }

    [Fact]
    public void MidnightToDateIsExtendedToEndOfDay()
    {
        var from = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc);

        var (normalizedFrom, normalizedTo) = ReportDateRange.Normalize(from, to);

        Assert.Equal(from, normalizedFrom);
        Assert.Equal(to.Date.AddDays(1).AddTicks(-1), normalizedTo);
        Assert.True(normalizedTo > new DateTime(2026, 10, 20, 23, 59, 59, DateTimeKind.Utc));
    }

    [Fact]
    public void ReversedDateRangeIsRejected()
    {
        Assert.Throws<BusinessRuleException>(() =>
            ReportDateRange.Normalize(new DateTime(2026, 10, 2), new DateTime(2026, 10, 1)));
    }

    [Fact]
    public void RangeOver366DaysIsRejected()
    {
        Assert.Throws<BusinessRuleException>(() =>
            ReportDateRange.Normalize(new DateTime(2025, 1, 1), new DateTime(2026, 10, 1)));
    }

    [Theory]
    [InlineData(1, PaymentRiskLevel.Low, false)]
    [InlineData(49_999.99, PaymentRiskLevel.Low, false)]
    [InlineData(50_000, PaymentRiskLevel.Medium, true)]
    [InlineData(249_999.99, PaymentRiskLevel.Medium, true)]
    [InlineData(250_000, PaymentRiskLevel.High, true)]
    [InlineData(1_000_000, PaymentRiskLevel.Critical, true)]
    public void RiskPolicyBoundariesAreExplicit(decimal amount, PaymentRiskLevel risk, bool makerChecker)
    {
        Assert.Equal(risk, PaymentPolicy.RiskFor(amount));
        Assert.Equal(makerChecker, PaymentPolicy.RequiresMakerChecker(amount));
    }

    [Theory]
    [InlineData("short1", false)]
    [InlineData("nodigitshere", false)]
    [InlineData("12345678", false)]
    [InlineData("validPass1", true)]
    public void NewPasswordPolicyRequiresLengthLetterAndDigit(string password, bool valid)
    {
        Assert.Equal(valid, PasswordPolicy.Validate(password).Count == 0);
    }
}
