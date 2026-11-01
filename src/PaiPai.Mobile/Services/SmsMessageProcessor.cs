using System.Globalization;
using System.Text.RegularExpressions;
using PaiPai.Core;
using PaiPai.Data;

namespace PaiPai.Mobile.Services;

public sealed class SmsMessageProcessor
{
    readonly LedgerRepository _repository;
    readonly IBillReminderScheduler _scheduler;

    public SmsMessageProcessor(LedgerRepository repository, IBillReminderScheduler scheduler)
    {
        _repository = repository;
        _scheduler = scheduler;
    }

    public async Task<SmsImportStatus> ProcessAsync(long smsId, string sender, string body, DateTime receivedAt)
    {
        var billCandidate = BillMessageParser.Parse(body, receivedAt);
        if (billCandidate is { IsPaymentConfirmation: true } payment)
        {
            BillReminder? matchedBill = null;
            if (payment.Amount is decimal paidAmount)
                matchedBill = await _repository.TryMarkMatchingBillPaidAsync("Jio", paidAmount, payment.AccountHint);
            if (matchedBill is not null)
            {
                await _scheduler.CancelAsync(matchedBill.Id);
                var paidRaw = CreateRaw(smsId, sender, body, receivedAt, "Jio", payment.Amount);
                paidRaw.Status = SmsImportStatus.BillSaved;
                paidRaw.Reason = "Matched Jio payment confirmation to an existing bill";
                paidRaw.AccountHint = payment.AccountHint;
                return await _repository.SaveImportedSmsAsync(paidRaw, null);
            }

            var review = CreateRaw(smsId, sender, body, receivedAt, "Jio", payment.Amount);
            review.Status = SmsImportStatus.NeedsReview;
            review.Reason = "Jio payment confirmation needs a matching bill or transaction check";
            review.Direction = Direction.Debit;
            review.Method = "UPI";
            review.AccountHint = payment.AccountHint;
            return await _repository.SaveImportedSmsAsync(review, null);
        }

        var result = SmsParser.Classify(body);
        if (result.Disposition == SmsMessageDisposition.Ignore)
        {
            if (billCandidate is null)
            {
                if (result.Reason == "No transaction signal" && TryCreateFallbackReview(smsId, sender, body, receivedAt) is { } fallback)
                    return await _repository.SaveImportedSmsAsync(fallback, null);
                return SmsImportStatus.Ignored;
            }

            var bill = new BillReminder
            {
                Name = billCandidate.Name,
                Amount = billCandidate.Amount,
                DueDate = billCandidate.DueDate,
                Cadence = billCandidate.Cadence,
                AccountHint = billCandidate.AccountHint,
                BillingPeriod = billCandidate.BillingPeriod,
                DisconnectionDate = billCandidate.DisconnectionDate,
                Category = billCandidate.Category,
                Note = body,
                Status = BillStatus.Upcoming,
                ReminderEnabled = true,
                SourceSmsId = smsId
            };
            var status = await _repository.SaveImportedBillAsync(CreateRaw(smsId, sender, body, receivedAt, bill.Name, bill.Amount), bill);
            if (status == SmsImportStatus.BillSaved && bill.Id > 0) await _scheduler.ScheduleAsync(bill);
            return status;
        }

        var parsed = result.Transaction;
        var raw = new RawSmsMessage
        {
            SmsId = smsId,
            Sender = sender,
            Body = body,
            ReceivedAt = receivedAt,
            Status = result.Disposition == SmsMessageDisposition.NeedsReview
                ? SmsImportStatus.NeedsReview
                : SmsImportStatus.Ignored,
            Reason = result.Reason,
            Amount = parsed?.Amount,
            Direction = parsed?.Direction,
            Merchant = parsed?.Merchant ?? "",
            Method = parsed?.Method ?? "",
            AccountHint = parsed?.AccountHint ?? "",
            AccountName = ResolveBankName(sender),
            BalanceAfter = parsed?.Balance,
            Reference = parsed?.Reference ?? ""
        };

        LedgerEntry? entry = result.Disposition == SmsMessageDisposition.Transaction && parsed is not null
            ? new LedgerEntry
            {
                Amount = parsed.Amount,
                Direction = parsed.Direction,
                Date = receivedAt,
                Merchant = parsed.Merchant,
                Category = Categorizer.Categorize(parsed.Merchant),
                Method = parsed.Method,
                AccountHint = parsed.AccountHint,
                AccountName = ResolveBankName(sender),
                Note = body,
                BalanceAfter = parsed.Balance,
                Reference = parsed.Reference,
                Source = EntrySource.Sms,
                SmsId = smsId,
                Confirmed = true
            }
            : null;

        return await _repository.SaveImportedSmsAsync(raw, entry);
    }

    static RawSmsMessage CreateRaw(long smsId, string sender, string body, DateTime receivedAt, string name, decimal? amount) => new()
    {
        SmsId = smsId,
        Sender = sender,
        Body = body,
        ReceivedAt = receivedAt,
        Status = SmsImportStatus.BillSaved,
        Merchant = name,
        Amount = amount
    };

    static string ResolveBankName(string sender)
    {
        var normalized = new string(sender.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        (string Token, string Name)[] banks =
        [
            ("HDFCBK", "HDFC Bank"), ("HDFCBANK", "HDFC Bank"),
            ("ICICIB", "ICICI Bank"), ("ICICIBANK", "ICICI Bank"),
            ("SBIINB", "State Bank of India"), ("SBIBNK", "State Bank of India"),
            ("AXISBK", "Axis Bank"), ("KOTAKB", "Kotak Mahindra Bank"),
            ("YESBANK", "Yes Bank"), ("PNBSMS", "Punjab National Bank"),
            ("IDFCFB", "IDFC FIRST Bank"), ("CANBNK", "Canara Bank"),
            ("BOIIND", "Bank of India"), ("UNIONB", "Union Bank of India")
        ];
        return banks.FirstOrDefault(x => normalized.Contains(x.Token, StringComparison.Ordinal)).Name ?? "";
    }

    static RawSmsMessage? TryCreateFallbackReview(long smsId, string sender, string body, DateTime receivedAt)
    {
        var money = Regex.Match(body, @"(?:\bRs\.?\s*:?\s*|\bINR\s*|\u20B9\s*)([\d,]+(?:\.\d{1,2})?)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (!money.Success || Regex.IsMatch(body, @"\b(?:otp|offer|cashback|coupon|reward|promotion|discount)\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            return null;
        var bankName = ResolveBankName(sender);
        var hasFinancialContext = Regex.IsMatch(body,
            @"\b(?:a/c|ac|account|card|upi|txn|transaction|purchase|debited|credited|balance|withdraw(?:n|al)?|payment)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (bankName.Length == 0 && !hasFinancialContext) return null;
        if (!decimal.TryParse(money.Groups[1].Value.Replace(",", "", StringComparison.Ordinal), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var amount) || amount <= 0) return null;

        Direction? direction = Regex.IsMatch(body, @"\b(debit(?:ed)?|deduct(?:ed)?|spent|paid|purchase|withdraw(?:n|al)?)\b", RegexOptions.IgnoreCase)
            ? Direction.Debit
            : Regex.IsMatch(body, @"\b(credit(?:ed)?|received|deposited|refund(?:ed)?)\b", RegexOptions.IgnoreCase)
                ? Direction.Credit : null;
        var account = Regex.Match(body, @"(?:a/c|ac|acct|account|card)\s*(?:no\.?\s*)?(?:ending(?:\s+with)?\s*)?[x*# .\-\d]*?(\d{3,4})(?!\d)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var method = Regex.IsMatch(body, @"\bupi\b", RegexOptions.IgnoreCase) ? "UPI"
            : Regex.IsMatch(body, @"\b(card|pos)\b", RegexOptions.IgnoreCase) ? "Card" : "Other";
        return new RawSmsMessage
        {
            SmsId = smsId, Sender = sender, Body = body, ReceivedAt = receivedAt,
            Status = SmsImportStatus.NeedsReview,
            Reason = "Possible bank transaction; confirm its type and details",
            Amount = amount, Direction = direction, Merchant = bankName.Length == 0 ? "Unknown" : bankName,
            Method = method, AccountHint = account.Success ? account.Groups[1].Value : "", AccountName = bankName
        };
    }
}
