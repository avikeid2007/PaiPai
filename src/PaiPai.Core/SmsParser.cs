using System.Globalization;
using System.Text.RegularExpressions;

namespace PaiPai.Core;

public enum SmsMessageDisposition { Transaction, NeedsReview, Ignore }

public sealed record ParsedSms(decimal Amount, Direction Direction, string Merchant, string Method,
                               string AccountHint, decimal? Balance, string Reference);

public sealed record SmsParseResult(SmsMessageDisposition Disposition, ParsedSms? Transaction, string Reason);

/// <summary>Conservative rules-based parser for transaction messages. Unclear financial messages are kept for review.</summary>
public static class SmsParser
{
    const RegexOptions O = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    const decimal AutomaticAmountLimit = 10_000_000m; // Outliers remain available in the raw-message review queue.

    static readonly Regex Failed = new(@"\b(failed|declined|unsuccessful|returned\s+unpaid)\b", O);
    static readonly Regex ObviousNonTransaction = new(
        @"\b(?:otp|spent\s+.+\s+this\s+month|spend\s+analysis|will\s+be\s+(?:debited|deducted|charged|withdrawn)|due\s+on|cashback\s+credited|flat\s+rs\.?\s*[\d,]+(?:\.\d{1,2})?\s+off|(?:i|we)\s+(?:paid|sent|transferred)\s+you)\b|\bif\s+not\s+you\b.{0,80}\bclick\b.{0,60}\b(?:block|verify|secure)\b",
        O);
    static readonly Regex DebitWord = new(@"\b(debit(?:ed)?|deduct(?:ed)?|spent|paid|paying|sent|withdraw(?:n|al)?|purchase|charged|used)\b", O);
    static readonly Regex CreditWord = new(@"\b(credited|received|deposited|refund(?:ed)?)\b", O);
    static readonly Regex ReversalWord = new(@"\b(reversed|reversal)\b", O);
    static readonly Regex AmountRx = new(@"(?:\brs\.?\s*:?[ \t]*|\binr\s*|₹\s*)([\d,]+(?:\.\d{1,2})?)", O);
    static readonly Regex AmountAfterVerbRx = new(@"\b(?:debit(?:ed)?|deduct(?:ed)?|spent|paid|paying|sent|withdraw(?:n|al)?|charged|revers(?:ed|al))\s+(?:(?:by|of)\s+)?([\d,]+(?:\.\d{1,2})?)|\b(?:transaction|txn)\s+of\s+([\d,]+(?:\.\d{1,2})?)", O);
    static readonly Regex BalanceRx = new(@"(?:avl|available|bal(?:ance)?)[^\d₹]*?(?:rs\.?\s*:?[ \t]*|inr\s*|₹\s*)?([\d,]+(?:\.\d{1,2})?)", O);
    static readonly Regex AccountRx = new(@"(?:a/c|ac|acct|account|card)\s*(?:no\.?\s*)?(?:ending(?:\s+with)?\s*)?[x*# .\-\d]*?(\d{3,4})(?!\d)", O);
    static readonly Regex RefRx = new(@"(?:ref(?:erence)?(?:\s*no\.?)?|utr|rrn|txn\s*id)\s*[:\-]?\s*([\w-]{6,})|\bUPI/P2M/([\w-]{6,})", O);
    const string Tail = @"\s+(?:VPA\s+)?([\p{L}\p{N}@._&' \-]{2,48}?)(?=\s+(?:on|via|ref(?:no)?|upi|using|from|bal|avl|if|dated|for|with|in)\b|\s*[.,(;]|$)";
    static readonly Regex DebitMerchant = new(@"\b(?:at|to|towards)" + Tail, O);
    static readonly Regex CreditMerchant = new(@"\b(?:from|by)" + Tail, O);
    static readonly Regex VpaMerchant = new(@"\bVPA\s+([\p{L}\p{N}._-]+)@[\w.-]+", O);
    static readonly Regex UpiPathMerchant = new(@"\bUPI/P2M/[^/\s]+/([\p{L}\p{N}._-]+)", O);
    static readonly Regex OrderMerchant = new(@"\bYour\s+([\p{L}][\p{L}\p{N}&' .-]{1,28}?)\s+Order\s+#", O);
    static readonly Regex SalaryMerchant = new(@"\(([^()]{2,48}\b(?:salary|payroll)\b[^()]*)\)", O);
    static readonly Regex TowardsMerchant = new(@"\btowards\s+([\p{L}\p{N}][\p{L}\p{N}&' .-]{1,44}?)(?=\s+(?:in|on|via|ref(?:no)?|upi|bal|avl)\b|[.,;]|$)", O);
    static readonly Regex RefundMerchant = new(@"\brefund\s+from\s+([\p{L}\p{N}][\p{L}\p{N}&' .-]{1,40}?)(?=\s+(?:ref(?:no)?|on|via|upi|bal|avl)\b|[.,;]|$)", O);
    static readonly Regex CardDebitPhrase = new(@"\busing\b.{0,48}\bdebit\s+card\b.{0,80}\b(?:for|amount|purchase)\b", O);
    static readonly Regex CardLimitMerchant = new(@"\b\d{1,2}[-/]\d{1,2}[-/]\d{2,4}\s+\d{1,2}:\d{2}(?::\d{2})?\s+([\p{L}\p{N}][\p{L}\p{N}&' .-]{1,40}?)\s+Avl\s+Lmt\b", O);
    static readonly Regex FinancialSignal = new(@"\b(?:debit(?:ed)?|deduct(?:ed)?|credit(?:ed)?|received|deposit(?:ed)?|spent|paid|paying|sent|withdraw(?:n|al)?|purchase|charged|refund(?:ed)?|reversed?|reversal|transaction|txn|utr)\b", O);

    /// <summary>Returns Transaction for a clear completed transaction, NeedsReview for an unclear financial message, and Ignore for non-transactions.</summary>
    public static SmsParseResult Classify(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return new(SmsMessageDisposition.Ignore, null, "Empty message");

        var normalized = NormalizeBody(body);
        if (Failed.IsMatch(normalized) || ObviousNonTransaction.IsMatch(normalized))
            return new(SmsMessageDisposition.Ignore, null, "Failed, future, summary, promotional, or non-bank message");

        if (!FinancialSignal.IsMatch(normalized))
            return new(SmsMessageDisposition.Ignore, null, "No transaction signal");

        var parsed = ParseTransaction(normalized);
        if (parsed is null)
            return new(SmsMessageDisposition.NeedsReview, null, "Financial message could not be parsed safely");
        return ReversalWord.IsMatch(normalized)
            ? new(SmsMessageDisposition.NeedsReview, parsed, "Reversal needs matching to the original transaction")
            : new(SmsMessageDisposition.Transaction, parsed, "Parsed transaction");
    }

    /// <summary>Returns a parsed transaction, or null when the message should be ignored or reviewed.</summary>
    public static ParsedSms? Parse(string? body)
    {
        var result = Classify(body);
        return result.Disposition == SmsMessageDisposition.Transaction ? result.Transaction : null;
    }

    static ParsedSms? ParseTransaction(string body)
    {
        var debit = DebitWord.Match(body);
        var credit = CreditWord.Match(body);
        var reversal = ReversalWord.Match(body);
        var cardDebit = CardDebitPhrase.Match(body);
        if (!debit.Success && !credit.Success && !reversal.Success && !cardDebit.Success) return null;

        var direction = reversal.Success
            ? Direction.Credit
            : cardDebit.Success && !debit.Success && !credit.Success
                ? Direction.Debit
                : debit.Success && (!credit.Success || debit.Index < credit.Index)
                    ? Direction.Debit
                    : Direction.Credit;

        var amount = FindTransactionAmount(body);
        if (amount is null or <= 0 || amount > AutomaticAmountLimit) return null;

        var balanceMatch = BalanceRx.Match(body);
        decimal? balance = balanceMatch.Success && TryToDecimal(balanceMatch.Groups[1].Value, out var parsedBalance)
            ? parsedBalance
            : null;

        var lower = body.ToLowerInvariant();
        var method = lower.Contains("upi", StringComparison.Ordinal) ? "UPI"
            : lower.Contains("atm", StringComparison.Ordinal) ? "ATM"
            : Regex.IsMatch(lower, @"\b(card|pos)\b") ? "Card"
            : Regex.IsMatch(lower, @"\b(neft|imps|rtgs|net\s*banking)\b") ? "Transfer"
            : Regex.IsMatch(lower, @"\b(cheque|chq)\b") ? "Cheque"
            : lower.Contains("wallet", StringComparison.Ordinal) ? "Wallet"
            : "Other";

        var account = AccountRx.Match(body);
        var reference = RefRx.Match(body);
        return new ParsedSms(amount.Value, direction,
            FindMerchant(body, direction), method,
            account.Success ? account.Groups[1].Value : "", balance,
            reference.Success ? (reference.Groups[1].Success ? reference.Groups[1].Value : reference.Groups[2].Value) : "");
    }

    static decimal? FindTransactionAmount(string body)
    {
        foreach (Match match in AmountRx.Matches(body))
        {
            var prefix = body[Math.Max(0, match.Index - 32)..match.Index];
            if (Regex.IsMatch(prefix, @"\b(avl|available|bal(?:ance)?|limit|lmt)\b", O)) continue;
            if (TryToDecimal(match.Groups[1].Value, out var amount)) return amount;
        }

        foreach (Match match in AmountAfterVerbRx.Matches(body))
        {
            var prefix = body[Math.Max(0, match.Index - 32)..match.Index];
            if (Regex.IsMatch(prefix, @"\b(avl|available|bal(?:ance)?|limit|lmt)\b", O)) continue;
            var amountText = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            if (TryToDecimal(amountText, out var amount)) return amount;
        }
        return null;
    }

    static string FindMerchant(string body, Direction direction)
    {
        if (Regex.IsMatch(body, @"\bpayment\b.{0,32}\bcredited\b.{0,24}\bcredit\s+card\b", O))
            return "CREDIT CARD PAYMENT";
        if (Regex.IsMatch(body, @"\bannual\s+fee\b", O))
            return "DEBIT CARD ANNUAL FEE";

        var special = RefundMerchant.Match(body);
        if (special.Success) return CleanMerchant(special.Groups[1].Value);

        special = VpaMerchant.Match(body);
        if (special.Success) return CleanMerchant(special.Groups[1].Value);
        special = UpiPathMerchant.Match(body);
        if (special.Success) return CleanMerchant(special.Groups[1].Value);
        special = OrderMerchant.Match(body);
        if (special.Success) return CleanMerchant(special.Groups[1].Value);
        special = SalaryMerchant.Match(body);
        if (special.Success) return CleanMerchant(special.Groups[1].Value);
        special = CardLimitMerchant.Match(body);
        if (special.Success) return CleanMerchant(special.Groups[1].Value);
        special = TowardsMerchant.Match(body);
        if (special.Success) return CleanMerchant(special.Groups[1].Value);

        var regex = direction == Direction.Debit ? DebitMerchant : CreditMerchant;
        foreach (Match match in regex.Matches(body))
        {
            var merchant = CleanMerchant(match.Groups[1].Value);
            if (!string.IsNullOrWhiteSpace(merchant) && !IsNonMerchant(merchant)) return merchant;
        }
        return "Unknown";
    }

    static string CleanMerchant(string value)
    {
        var result = Regex.Replace(value.Trim(), @"\s+", " ").Trim(' ', '-', '.', ',', ';', ':');
        var vpaAt = result.IndexOf('@');
        if (vpaAt > 0) result = result[..vpaAt];
        return result;
    }

    static bool IsNonMerchant(string name) => Regex.IsMatch(name,
        @"^(?:xx.*|x{2,}\d*|(?:a/c|ac|your|account|the|from|to)\b.*|card|upi|neft|imps|rtgs|netbanking)$", O);

    static string NormalizeBody(string body) => body
        .Replace("\\n", " ", StringComparison.OrdinalIgnoreCase)
        .Replace("\\r", " ", StringComparison.OrdinalIgnoreCase);

    static bool TryToDecimal(string text, out decimal value) =>
        decimal.TryParse(text.Replace(",", "", StringComparison.Ordinal), NumberStyles.Number,
            CultureInfo.InvariantCulture, out value);
}
