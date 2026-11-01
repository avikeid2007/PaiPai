using System.Globalization;
using System.Text.RegularExpressions;

namespace PaiPai.Core;

/// <summary>Conservative parser for bill reminders. Bill notices never become expenses.</summary>
public static class BillMessageParser
{
    const RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    static readonly Regex Amount = new(@"(?:\brs\.?\s*:?[ \t]*|\binr\s*|\u20b9\s*)([\d,]+(?:\.\d{1,2})?)", O);
    static readonly Regex DateRx = new(@"\b(\d{1,2})[-/](\d{1,2})[-/](\d{2,4})\b", O);
    static readonly Regex MobileRx = new(@"(?:Jio\s+Number|mobile\s*(?:no\.?|number)?|\u0915\u0928\u0947\u0915\u094d\u0936\u0928)\s*:?\s*(?:\+?91[- ]?)?(\d{10,12})", O);
    static readonly Regex UppclAmount = new(@"\u0915\u0941\u0932\s*\u092c\u0915\u093e\u092f\u093e\s*:?\s*(?:\u20b9\s*)?([\d,]+(?:\.\d{1,2})?)", O);
    static readonly Regex UppclDue = new(@"\u0926\u0947\u092f\s*\u0924\u093f\u0925\u093f\s*:?\s*(\d{1,2}[-/]\d{1,2}[-/]\d{4})", O);
    static readonly Regex UppclDisconnect = new(@"\u0935\u093f\u091a\u094d\u091b\u0947\u0926\u0928\s*\u0924\u093f\u0925\u093f\s*:?\s*(\d{1,2}[-/]\d{1,2}[-/]\d{4})", O);
    static readonly Regex UppclPeriod = new(@"\u092e\u093e\u0939\s+([A-Za-z]+[- ]\d{4})", O);

    public static ParsedBillReminder? Parse(string? body, DateTime receivedAt)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        var mobile = MobileRx.Match(body);
        var account = mobile.Success ? mobile.Groups[1].Value : "";

        // Jio payment confirmation: keep it as bill evidence, never create a second expense.
        if (Regex.IsMatch(body, @"\bPayment\s+of\s+Rs\.?\s*[\d,]+(?:\.\d{1,2})?\s+has\s+been\s+received\s+on\b", O))
        {
            var amount = ParseAmount(body, Amount);
            var dateMatch = Regex.Match(body, @"has\s+been\s+received\s+on\s+(\d{1,2}[-\s][A-Za-z]{3,9}[-,\s]\d{2,4})", O);
            var date = dateMatch.Success ? ParseTextDate(dateMatch.Groups[1].Value) : null;
            if (amount is null || date is null) return null;
            return new ParsedBillReminder("Jio", amount, date.Value, "One time", account,
                date.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture), IsPaymentConfirmation: true);
        }

        // Jio e-bill generation.
        if (Regex.IsMatch(body, @"e-bill\s+for\s+Jio\s+Number", O))
        {
            var amount = ParseAmountAfter(body, @"Total\s+Amount\s+payable\s*:");
            var due = ParseDateAfter(body, @"Payment\s+due\s+date\s*:");
            if (due is null) return null;
            var period = Regex.Match(body, @"Bill\s+period\s*:\s*(\d{1,2}-[A-Za-z]{3,9}-\d{2,4})\s+to\s+(\d{1,2}-[A-Za-z]{3,9}-\d{2,4})", O);
            var periodKey = period.Success ? $"{NormalizeDate(period.Groups[1].Value):yyyy-MM-dd}/{NormalizeDate(period.Groups[2].Value):yyyy-MM-dd}" : due.Value.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var billDate = Regex.Match(body, @"(\d{1,2}-[A-Za-z]{3,9}-\d{2,4})\s+e-bill", O);
            return new ParsedBillReminder("Jio", amount, due.Value, "One time", account, periodKey, Category: "Utilities / Mobile");
        }

        // UPPCL Hindi electricity bill reminder. Month and connection identify repeat notices.
        var hindiDue = UppclDue.Match(body);
        var hindiAmount = UppclAmount.Match(body);
        if (hindiDue.Success && hindiAmount.Success)
        {
            if (!TryParseAmount(hindiAmount.Groups[1].Value, out var value) || !TryParseDate(hindiDue.Groups[1].Value, out var dueDate)) return null;
            var disconnection = UppclDisconnect.Match(body);
            DateTime? disconnectDate = disconnection.Success && TryParseDate(disconnection.Groups[1].Value, out var parsedDisconnect) ? parsedDisconnect : null;
            var periodMatch = UppclPeriod.Match(body);
            var periodKey = periodMatch.Success ? NormalizePeriod(periodMatch.Groups[1].Value) : dueDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            return new ParsedBillReminder("UPPCL Electricity", value, dueDate, "One time", account, periodKey, disconnectDate, Category: "Utilities / Electricity");
        }

        // General bill reminder messages supported by the existing parser.
        if (!Regex.IsMatch(body, @"\b(?:due|reminder|bill|emi|autopay|subscription|sip|payment\s+by)\b", O)
            || Regex.IsMatch(body, @"\b(?:paid|debited|deducted|failed|declined|cancelled|canceled|completed)\b", O)) return null;
        var genericDue = FindDueDate(body, receivedAt);
        if (genericDue is null) return null;
        var genericName = Regex.Match(body, @"\b(?:towards|for)\s+(?:your\s+)?([\p{L}\p{N}][\p{L}\p{N}&' .-]{1,48}?)(?=\s+(?:on|by|due|before|from|a/c|account|card|subscription|emi|statement|bill|ref)\b|[.,;]|$)", O);
        var name = genericName.Success ? Regex.Replace(genericName.Groups[1].Value.Trim(), @"\s+", " ") : "Bill reminder";
        var cadence = Regex.IsMatch(body, @"\b(monthly|every\s+month|subscription|autopay|emi|sip)\b", O) ? "Monthly"
            : Regex.IsMatch(body, @"\b(yearly|annual|every\s+year)\b", O) ? "Yearly" : "One time";
        return new ParsedBillReminder(name, ParseAmount(body, Amount), genericDue.Value, cadence);
    }

    static DateTime? FindDueDate(string body, DateTime receivedAt)
    {
        var relative = Regex.Match(body, @"\b(today|tomorrow)\b", O);
        if (relative.Success) return relative.Value.Equals("tomorrow", StringComparison.OrdinalIgnoreCase) ? receivedAt.Date.AddDays(1) : receivedAt.Date;
        var after = ParseDateAfter(body, @"\b(?:due\s+date|due\s+on|pay\s+by|payment\s+by)\s*[:\-]?");
        if (after is not null) return after;
        var numeric = DateRx.Match(body);
        if (numeric.Success && TryParseDate(numeric.Value, out var date)) return date;
        var text = Regex.Match(body, @"\b\d{1,2}[-\s][A-Za-z]{3,9}[-,\s]\d{2,4}\b", O);
        return text.Success ? ParseTextDate(text.Value) : null;
    }

    static decimal? ParseAmount(string body, Regex regex)
    {
        var match = regex.Match(body);
        return match.Success && TryParseAmount(match.Groups[1].Value, out var value) ? value : null;
    }

    static decimal? ParseAmountAfter(string body, string label)
    {
        var match = Regex.Match(body, label + @"\s*(?:Rs\.?|INR|\u20b9)?\s*([\d,]+(?:\.\d{1,2})?)", O);
        return match.Success && TryParseAmount(match.Groups[1].Value, out var value) ? value : null;
    }

    static DateTime? ParseDateAfter(string body, string label)
    {
        var match = Regex.Match(body, label + @"\s*(\d{1,2}[-/][A-Za-z0-9-]+)", O);
        if (!match.Success) return null;
        return ParseFlexibleDate(match.Groups[1].Value);
    }

    static DateTime? ParseFlexibleDate(string value) => TryParseDate(value, out var d) ? d : ParseTextDate(value);

    static DateTime? ParseTextDate(string value)
    {
        return DateTime.TryParseExact(value.Trim().TrimEnd('.'),
            ["d-MMM-yy", "d-MMM-yyyy", "d MMM yy", "d MMM yyyy", "d-MMMM-yy", "d-MMMM-yyyy", "d MMMM yy", "d MMMM yyyy"],
            CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var result) ? result : null;
    }

    static DateTime NormalizeDate(string value) => ParseTextDate(value) ?? DateTime.MinValue;

    static bool TryParseDate(string value, out DateTime result)
    {
        foreach (var format in new[] { "d-M-yyyy", "dd-MM-yyyy", "d/M/yyyy", "dd/MM/yyyy" })
            if (DateTime.TryParseExact(value, format, CultureInfo.InvariantCulture, DateTimeStyles.None, out result)) return true;
        result = default;
        return false;
    }

    static string NormalizePeriod(string value)
    {
        var match = Regex.Match(value, @"^([A-Za-z]+)[- ](\d{4})$");
        return match.Success && DateTime.TryParseExact(match.Groups[1].Value, "MMMM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var month)
            ? $"{match.Groups[2].Value}-{month.Month:00}" : value.Trim();
    }

    static bool TryParseAmount(string text, out decimal amount) => decimal.TryParse(
        text.Replace(",", "", StringComparison.Ordinal), NumberStyles.Number, CultureInfo.InvariantCulture, out amount);
}
