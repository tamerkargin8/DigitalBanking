namespace DigitalBanking.Domain.Enums;

/// <summary>
/// Account product type. Time deposit and investment are management-only demo
/// products; no interest accrual, trading or FX return is calculated.
/// </summary>
public enum AccountType
{
    Checking = 1,
    TimeDeposit = 2,
    Investment = 3
}
