namespace DigitalBanking.Domain.Enums;

public enum ReconciliationMatchStatus
{
    Unmatched = 0,
    Matched = 1,
    OnlyExternal = 2,
    OnlySystem = 3,
    AmountMismatch = 4,
    CurrencyMismatch = 5,
    DuplicateReference = 6
}
