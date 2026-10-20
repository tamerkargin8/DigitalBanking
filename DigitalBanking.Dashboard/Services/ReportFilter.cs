using System.Globalization;

namespace DigitalBanking.Dashboard.Services;

/// <summary>
/// Date/limit filter used by report screens. Kept separate from the page so the
/// same boundary rules can be unit tested.
/// </summary>
public static class ReportFilter
{
    public static bool TryParse(
        string? from,
        string? to,
        string? limit,
        out DateTime fromDate,
        out DateTime toDate,
        out int take,
        out string? error)
    {
        fromDate = DateTime.UtcNow.Date.AddDays(-30);
        toDate = DateTime.UtcNow.Date;
        take = 10;
        error = null;

        if (!string.IsNullOrWhiteSpace(from)
            && !DateTime.TryParseExact(from, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fromDate))
        {
            error = "Başlangıç tarihi geçerli değil (yyyy-MM-dd).";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(to)
            && !DateTime.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out toDate))
        {
            error = "Bitiş tarihi geçerli değil (yyyy-MM-dd).";
            return false;
        }

        if (toDate < fromDate)
        {
            error = "Bitiş tarihi başlangıçtan önce olamaz.";
            return false;
        }

        if ((toDate - fromDate).TotalDays > 366)
        {
            error = "Tarih aralığı 366 günü aşamaz.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(limit))
        {
            if (!int.TryParse(limit, NumberStyles.Integer, CultureInfo.InvariantCulture, out take))
            {
                error = "Adet sayısal olmalıdır.";
                return false;
            }

            take = Math.Clamp(take, 1, 50);
        }

        return true;
    }

    public static string ToInputValue(DateTime value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
