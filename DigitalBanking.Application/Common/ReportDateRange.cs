using DigitalBanking.Application.Exceptions;

namespace DigitalBanking.Application.Common;

/// <summary>
/// Normalizes report date ranges. A midnight <c>to</c> value is treated as the end
/// of that calendar day (inclusive), which is what date-only UI filters expect.
/// </summary>
public static class ReportDateRange
{
    public static readonly TimeSpan MaximumRange = TimeSpan.FromDays(366);

    public static (DateTime From, DateTime To) Normalize(DateTime from, DateTime to)
    {
        if (to.TimeOfDay == TimeSpan.Zero)
            to = to.Date.AddDays(1).AddTicks(-1);

        if (to < from)
            throw new BusinessRuleException("Bitiş tarihi başlangıç tarihinden önce olamaz.");

        if (to - from > MaximumRange)
            throw new BusinessRuleException("Tarih aralığı 366 günü aşamaz.");

        return (from, to);
    }
}
