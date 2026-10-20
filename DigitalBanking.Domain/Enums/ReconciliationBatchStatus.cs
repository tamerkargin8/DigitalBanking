namespace DigitalBanking.Domain.Enums;

public enum ReconciliationBatchStatus
{
    Previewed = 1,
    Invalid = 2,
    Imported = 3,
    Failed = 4,
    Importing = 5
}
