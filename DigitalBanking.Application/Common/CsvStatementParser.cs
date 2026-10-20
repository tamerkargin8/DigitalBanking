using System.Globalization;
using System.Text;
using DigitalBanking.Application.Common;
using DigitalBanking.Domain.Enums;

namespace DigitalBanking.Application.Common;

public sealed class CsvStatementRow
{
    public int LineNumber { get; init; }
    public string ExternalReference { get; init; } = string.Empty;
    public string InternalReference { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public DateTime TransactionDate { get; init; }
    public ReconciliationDirection Direction { get; init; }
    public bool IsValid { get; init; }
    public string? Error { get; init; }
}

public sealed class CsvStatementParseResult
{
    public string? FatalError { get; init; }
    public List<CsvStatementRow> Rows { get; init; } = new();
}

/// <summary>
/// Strict CSV parser for the synthetic external statement format. It supports
/// quoted fields, escaped quotes and embedded newlines; it never treats a partial
/// or malformed file as success.
/// </summary>
public static class CsvStatementParser
{
    public const int MaxFileBytes = 2 * 1024 * 1024;
    public const int MaxRows = 5_000;

    public static readonly string[] RequiredHeaders =
    {
        "external_reference",
        "internal_reference",
        "amount",
        "currency",
        "transaction_date",
        "direction"
    };

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public static CsvStatementParseResult Parse(byte[] bytes)
    {
        if (bytes.Length == 0)
            return Fatal("Dosya boş.");
        if (bytes.Length > MaxFileBytes)
            return Fatal($"Dosya boyutu {MaxFileBytes / (1024 * 1024)} MB sınırını aşıyor.");

        string text;
        try
        {
            text = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Fatal("Dosya geçerli UTF-8 değil.");
        }

        text = text.TrimStart('\uFEFF');

        List<ParsedRecord> records;
        try
        {
            records = ParseRecords(text);
        }
        catch (FormatException ex)
        {
            return Fatal(ex.Message);
        }

        var nonEmpty = records.Where(r => r.Fields.Any(f => !string.IsNullOrWhiteSpace(f))).ToList();
        if (nonEmpty.Count == 0)
            return Fatal("Başlık satırı bulunamadı.");

        var header = nonEmpty[0];
        var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Fields.Count; i++)
        {
            var name = header.Fields[i].Trim().ToLowerInvariant();
            if (name.Length == 0) continue;
            if (!headerMap.TryAdd(name, i))
                return Fatal($"Başlıkta yinelenen alan: {name}.");
        }

        if (headerMap.Count != RequiredHeaders.Length
            || RequiredHeaders.Any(required => !headerMap.ContainsKey(required)))
        {
            return Fatal("Başlık tam olarak şu alanları içermelidir: " + string.Join(", ", RequiredHeaders) + ".");
        }

        var dataRecords = nonEmpty.Skip(1).ToList();
        if (dataRecords.Count == 0)
            return Fatal("Dosyada veri satırı yok.");
        if (dataRecords.Count > MaxRows)
            return Fatal($"Satır sayısı {MaxRows} sınırını aşıyor.");

        var result = new List<CsvStatementRow>(dataRecords.Count);
        foreach (var record in dataRecords)
        {
            result.Add(ParseRow(record, headerMap));
        }

        return new CsvStatementParseResult { Rows = result };
    }

    private static CsvStatementRow ParseRow(ParsedRecord record, IReadOnlyDictionary<string, int> headerMap)
    {
        string Field(string name) =>
            headerMap.TryGetValue(name, out var index) && index < record.Fields.Count
                ? record.Fields[index].Trim()
                : string.Empty;

        var external = Field("external_reference");
        var internalReference = Field("internal_reference");
        var amountText = Field("amount");
        var currencyText = Field("currency");
        var dateText = Field("transaction_date");
        var directionText = Field("direction");

        if (record.Fields.Count != RequiredHeaders.Length)
            return Invalid(record, external, internalReference, "Satırda beklenen alan sayısı yok (6 alan gerekir).");

        if (string.IsNullOrWhiteSpace(external) || external.Length > 100)
            return Invalid(record, external, internalReference, "external_reference zorunlu ve en fazla 100 karakter olmalı.");

        if (!Guid.TryParse(internalReference, out _))
            return Invalid(record, external, internalReference, "internal_reference geçerli bir GUID olmalı.");

        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            || amount <= 0
            || amount > Money.MaximumAmount
            || decimal.Round(amount, 2) != amount)
            return Invalid(record, external, internalReference, "amount pozitif, en fazla 2 ondalıklı decimal olmalı.");

        string currency;
        try
        {
            currency = CurrencyCodes.Normalize(currencyText);
        }
        catch (Exceptions.BusinessRuleException)
        {
            return Invalid(record, external, internalReference, "currency TRY, USD, EUR veya GBP olmalı.");
        }

        if (!TryParseDate(dateText, out var transactionDate))
            return Invalid(record, external, internalReference, "transaction_date yyyy-MM-dd veya ISO-8601 olmalı.");

        if (!Enum.TryParse<ReconciliationDirection>(directionText, ignoreCase: true, out var direction))
            return Invalid(record, external, internalReference, "direction Debit veya Credit olmalı.");

        return new CsvStatementRow
        {
            LineNumber = record.LineNumber,
            ExternalReference = external,
            InternalReference = Guid.Parse(internalReference).ToString("D"),
            Amount = amount,
            Currency = currency,
            TransactionDate = transactionDate,
            Direction = direction,
            IsValid = true
        };
    }

    private static CsvStatementRow Invalid(ParsedRecord record, string external, string internalReference, string error) => new()
    {
        LineNumber = record.LineNumber,
        ExternalReference = external.Length > 100 ? external[..100] : external,
        InternalReference = internalReference.Length > 100 ? internalReference[..100] : internalReference,
        IsValid = false,
        Error = error
    };

    public static bool TryParseDate(string value, out DateTime date)
    {
        date = default;
        if (string.IsNullOrWhiteSpace(value)) return false;

        var styles = DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal;
        if (DateTime.TryParseExact(value.Trim(), new[]
            {
                "yyyy-MM-dd",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-ddTHH:mm:ss",
                "yyyy-MM-ddTHH:mm:ssZ",
                "yyyy-MM-ddTHH:mm:ss.fffZ"
            }, CultureInfo.InvariantCulture, styles, out date))
            return true;

        return DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture, styles, out date);
    }

    private sealed record ParsedRecord(int LineNumber, List<string> Fields);

    private static List<ParsedRecord> ParseRecords(string text)
    {
        var records = new List<ParsedRecord>();
        var fields = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        var line = 1;
        var recordLine = 1;

        for (var i = 0; i < text.Length; i++)
        {
            var ch = text[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        field.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    if (ch == '\n') line++;
                    field.Append(ch);
                }

                continue;
            }

            switch (ch)
            {
                case '"':
                    if (field.Length != 0)
                        throw new FormatException($"Satır {line}: tırnak alan ortasında başlayamaz.");
                    inQuotes = true;
                    break;
                case ',':
                    fields.Add(field.ToString());
                    field.Clear();
                    break;
                case '\r':
                    if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add(new ParsedRecord(recordLine, fields));
                    fields = new List<string>();
                    line++;
                    recordLine = line;
                    break;
                case '\n':
                    fields.Add(field.ToString());
                    field.Clear();
                    records.Add(new ParsedRecord(recordLine, fields));
                    fields = new List<string>();
                    line++;
                    recordLine = line;
                    break;
                default:
                    field.Append(ch);
                    break;
            }
        }

        if (inQuotes)
            throw new FormatException($"Satır {recordLine}: kapanmamış tırnak.");

        if (field.Length > 0 || fields.Count > 0)
        {
            fields.Add(field.ToString());
            records.Add(new ParsedRecord(recordLine, fields));
        }

        return records;
    }

    private static CsvStatementParseResult Fatal(string error) => new() { FatalError = error };
}
