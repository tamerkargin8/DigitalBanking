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
            throw new BusinessRuleException("'to' date must be greater than or equal to 'from' date.");

        if (to - from > MaximumRange)
            throw new BusinessRuleException("Date range cannot exceed 366 days.");

        return (from, to);
    }
}
