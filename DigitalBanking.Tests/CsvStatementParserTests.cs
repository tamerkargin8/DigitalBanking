using System.Text;
using DigitalBanking.Application.Common;

namespace DigitalBanking.Tests;

public class CsvStatementParserTests
{
    private const string Header = "external_reference,internal_reference,amount,currency,transaction_date,direction\n";
    private static readonly Guid ValidGuid = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void ValidQuotedCsvIsParsedWithDecimalAndUtcDate()
    {
        var csv = Header + $"\"EXT,1\",{ValidGuid},100.25,USD,2026-10-20,Debit\n";
        var result = CsvStatementParser.Parse(Encoding.UTF8.GetBytes(csv));

        Assert.Null(result.FatalError);
        var row = Assert.Single(result.Rows);
        Assert.True(row.IsValid);
        Assert.Equal("EXT,1", row.ExternalReference);
        Assert.Equal(100.25m, row.Amount);
        Assert.Equal("USD", row.Currency);
        Assert.Equal(new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), row.TransactionDate);
        Assert.Equal(DigitalBanking.Domain.Enums.ReconciliationDirection.Debit, row.Direction);
    }

    [Fact]
    public void BOMAndCrLfAreAccepted()
    {
        var csv = "\uFEFF" + Header.Replace("\n", "\r\n") + $"{ValidGuid},{ValidGuid},1,TRY,2026-10-20,Credit\r\n";
        var result = CsvStatementParser.Parse(Encoding.UTF8.GetBytes(csv));
        Assert.Null(result.FatalError);
        Assert.Single(result.Rows);
        Assert.True(result.Rows[0].IsValid);
    }

    [Theory]
    [InlineData("wrong_header,internal_reference,amount,currency,transaction_date,direction\n")]
    [InlineData("external_reference,internal_reference,amount,currency,transaction_date\n")]
    [InlineData("")]
    public void MissingOrEmptyHeaderIsFatal(string content)
    {
        var result = CsvStatementParser.Parse(Encoding.UTF8.GetBytes(content));
        Assert.NotNull(result.FatalError);
        Assert.Empty(result.Rows);
    }

    [Fact]
    public void InvalidUtf8IsFatal()
    {
        var result = CsvStatementParser.Parse(new byte[] { 0xFF, 0xFE, 0x00 });
        Assert.NotNull(result.FatalError);
        Assert.Contains("UTF-8", result.FatalError);
    }

    [Fact]
    public void OversizedFileIsFatal()
    {
        var result = CsvStatementParser.Parse(new byte[CsvStatementParser.MaxFileBytes + 1]);
        Assert.NotNull(result.FatalError);
        Assert.Contains("sınır", result.FatalError);
    }

    [Fact]
    public void TooManyRowsIsFatal()
    {
        var builder = new StringBuilder(Header);
        for (var i = 0; i <= CsvStatementParser.MaxRows; i++)
            builder.Append($"{ValidGuid},{ValidGuid},1,TRY,2026-10-20,Debit\n");

        var result = CsvStatementParser.Parse(Encoding.UTF8.GetBytes(builder.ToString()));
        Assert.NotNull(result.FatalError);
        Assert.Contains("sınır", result.FatalError);
    }

    [Theory]
    [InlineData("EXT-1,not-a-guid,10,TRY,2026-10-20,Debit")]
    [InlineData("EXT-1,11111111-1111-1111-1111-111111111111,0,TRY,2026-10-20,Debit")]
    [InlineData("EXT-1,11111111-1111-1111-1111-111111111111,10.001,TRY,2026-10-20,Debit")]
    [InlineData("EXT-1,11111111-1111-1111-1111-111111111111,10,BTC,2026-10-20,Debit")]
    [InlineData("EXT-1,11111111-1111-1111-1111-111111111111,10,TRY,not-a-date,Debit")]
    [InlineData("EXT-1,11111111-1111-1111-1111-111111111111,10,TRY,2026-10-20,Sideways")]
    [InlineData(",11111111-1111-1111-1111-111111111111,10,TRY,2026-10-20,Debit")]
    [InlineData("EXT-1,11111111-1111-1111-1111-111111111111,10,TRY,2026-10-20")]
    public void InvalidRowsAreFlaggedNotSilentlyAccepted(string dataRow)
    {
        var result = CsvStatementParser.Parse(Encoding.UTF8.GetBytes(Header + dataRow + "\n"));
        Assert.Null(result.FatalError);
        var row = Assert.Single(result.Rows);
        Assert.False(row.IsValid);
        Assert.False(string.IsNullOrWhiteSpace(row.Error));
    }

    [Fact]
    public void UnclosedQuoteIsFatal()
    {
        var csv = Header + "\"EXT-1," + ValidGuid + ",10,TRY,2026-10-20,Debit\n";
        var result = CsvStatementParser.Parse(Encoding.UTF8.GetBytes(csv));
        Assert.NotNull(result.FatalError);
        Assert.Contains("tırnak", result.FatalError);
    }
}
