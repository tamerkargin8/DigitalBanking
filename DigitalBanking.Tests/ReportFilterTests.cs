using DigitalBanking.Dashboard.Services;

namespace DigitalBanking.Tests;

public class ReportFilterTests
{
    [Fact]
    public void MissingValuesUseLastThirtyDaysAndDefaultLimit()
    {
        Assert.True(ReportFilter.TryParse(null, null, null, out var from, out var to, out var take, out var error));
        Assert.Null(error);
        Assert.Equal(10, take);
        Assert.Equal(DateTime.UtcNow.Date, to);
        Assert.Equal(DateTime.UtcNow.Date.AddDays(-30), from);
    }

    [Fact]
    public void ValidDatesAndLimitAreAccepted()
    {
        Assert.True(ReportFilter.TryParse("2026-10-01", "2026-10-20", "25", out var from, out var to, out var take, out var error));
        Assert.Null(error);
        Assert.Equal(new DateTime(2026, 10, 1), from);
        Assert.Equal(new DateTime(2026, 10, 20), to);
        Assert.Equal(25, take);
    }

    [Fact]
    public void LimitIsClampedToFifty()
    {
        Assert.True(ReportFilter.TryParse("2026-10-01", "2026-10-20", "500", out _, out _, out var take, out _));
        Assert.Equal(50, take);
    }

    [Theory]
    [InlineData("not-a-date", "2026-10-20", "10")]
    [InlineData("2026-10-01", "not-a-date", "10")]
    [InlineData("2026-10-20", "2026-10-01", "10")]
    [InlineData("2025-01-01", "2026-10-01", "10")]
    [InlineData("2026-10-01", "2026-10-20", "abc")]
    public void InvalidFiltersReturnSafeMessages(string? from, string? to, string? limit)
    {
        Assert.False(ReportFilter.TryParse(from, to, limit, out _, out _, out _, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }
}
