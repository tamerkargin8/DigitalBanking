using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Application.Common;

/// <summary>Stable Turkish labels for accounting enums (UI and API share them).</summary>
public static class AccountingLabels
{
    public static string AccountType(JournalAccountType type) => type switch
    {
        JournalAccountType.CustomerLiability => "Müşteri Yükümlülüğü",
        JournalAccountType.CashClearing => "Kasa Karşı Hesabı",
        JournalAccountType.OpeningBalanceOffset => "Açılış Denkleştirme",
        _ => type.ToString()
    };

    public static string Side(JournalSide side) => side switch
    {
        JournalSide.Debit => "Borç",
        JournalSide.Credit => "Alacak",
        _ => side.ToString()
    };

    public static string Operation(JournalSourceOperation operation) => operation switch
    {
        JournalSourceOperation.Deposit => "Yatırma",
        JournalSourceOperation.Withdrawal => "Çekme",
        JournalSourceOperation.Transfer => "Transfer",
        JournalSourceOperation.AccountOpening => "Hesap Açılışı",
        JournalSourceOperation.Baseline => "Baseline",
        JournalSourceOperation.Reversal => "Ters Kayıt",
        _ => operation.ToString()
    };
}
