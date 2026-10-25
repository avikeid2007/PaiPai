using System.Globalization;
using System.Text.RegularExpressions;

namespace PaiPai.Core;

public static class BillMessageParser
{
    const RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    static readonly Regex DueSignal = new(@"\b(?:due|reminder|bill|emi|autopay|subscription|sip|payment\s+by)\b", O);
    static readonly Regex CompletedSignal = new(@"\b(?:paid|debited|deducted|failed|declined|cancelled|canceled|completed)\b", O);
    static readonly Regex NumericDate = new(@"\b(\d{1,2})[-/](\d{1,2})[-/](\d{2,4})\b", O);
    static readonly Regex TextDate = new(@"\b(\d{1,2})[-\s]([A-Za-z]{3,9})[-,\s](\d{2,4})\b", O);
    static readonly Regex IsoDate = new(@"\b(\d{4})-(\d{1,2})-(\d{1,2})\b", O);
    static readonly Regex RelativeDate = new(@"\b(today|tomorrow)\b", O);
    static readonly Regex Amount = new(@"(?:\brs\.?\s*:?[ \t]*|\binr\s*|₹\s*)([\d,]+(?:\.\d{1,2})?)", O);
    static readonly Regex Name = new(@"\b(?:towards|for)\s+(?:your\s+)?([\p{L}\p{N}][\p{L}\p{N}&' .-]{1,48}?)(?=\s+(?:on|by|due|before|from|a/c|account|card|subscription|emi|statement|bill|ref)\b|[.,;]|$)", O);
    static readonly Regex Account = new(@"(?:a/c|ac|acct|account|card)\s*(?:no\.?\s*)?(?:ending(?:\s+with)?\s*)?[x*# .\-\d]*?(\d{3,4})(?!\d)", O);

    public static ParsedBillReminder? Parse(string? body, DateTime receivedAt)
    {
        if (string.IsNullOrWhiteSpace(body) || !DueSignal.IsMatch(body) || CompletedSignal.IsMatch(body)) return null;
        var dueDate = FindDueDate(body, receivedAt);
        if (dueDate is null) return null;

        decimal? amount = null;
        var amountMatch = Amount.Match(body);
        if (amountMatch.Success && decimal.TryParse(amountMatch.Groups[1].Value.Replace(",", "", StringComparison.Ordinal),
                NumberStyles.Number, CultureInfo.InvariantCulture, out var value)) amount = value;

        var nameMatch = Name.Match(body);
        var name = nameMatch.Success
            ? Regex.Replace(nameMatch.Groups[1].Value.Trim(), @"\s+", " ")
            : "Bill reminder";
        var cadence = Regex.IsMatch(body, @"\b(monthly|every\s+month|subscription|autopay|emi|sip)\b", O) ? "Monthly"
            : Regex.IsMatch(body, @"\b(yearly|annual|every\s+year)\b", O) ? "Yearly"
            : "One time";
        return new ParsedBillReminder(name, amount, dueDate.Value, cadence);
    }

    static DateTime? FindDueDate(string body, DateTime receivedAt)
    {
        var relative = RelativeDate.Match(body);
        if (relative.Success)
            return relative.Groups[1].Value.Equals("tomorrow", StringComparison.OrdinalIgnoreCase)
                ? receivedAt.Date.AddDays(1)
                : receivedAt.Date;

        var iso = IsoDate.Match(body);
        if (iso.Success && DateTime.TryParseExact(iso.Value, "yyyy-M-d", CultureInfo.InvariantCulture, DateTimeStyles.None, out var isoDate))
            return isoDate;

        var numeric = NumericDate.Match(body);
        if (numeric.Success)
        {
            var day = int.Parse(numeric.Groups[1].Value, CultureInfo.InvariantCulture);
            var month = int.Parse(numeric.Groups[2].Value, CultureInfo.InvariantCulture);
            var year = ParseYear(numeric.Groups[3].Value);
            if (TryDate(year, month, day, out var result)) return result;
        }

        var text = TextDate.Match(body);
        if (text.Success && DateTime.TryParseExact(text.Value,
                ["d-MMM-yy", "d-MMM-yyyy", "d MMM yy", "d MMM yyyy", "d-MMMM-yy", "d-MMMM-yyyy", "d MMMM yy", "d MMMM yyyy"],
                CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var textDate)) return textDate;
        return null;
    }

    static int ParseYear(string value)
    {
        var year = int.Parse(value, CultureInfo.InvariantCulture);
        return value.Length == 2 ? year + (year < 70 ? 2000 : 1900) : year;
    }

    static bool TryDate(int year, int month, int day, out DateTime result)
    {
        try { result = new DateTime(year, month, day); return true; }
        catch (ArgumentOutOfRangeException) { result = default; return false; }
    }
}
