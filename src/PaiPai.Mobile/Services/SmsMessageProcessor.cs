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
        var result = SmsParser.Classify(body);
        if (result.Disposition == SmsMessageDisposition.Ignore)
        {
            var billCandidate = BillMessageParser.Parse(body, receivedAt);
            if (billCandidate is null) return SmsImportStatus.Ignored;

            var bill = new BillReminder
            {
                Name = billCandidate.Name,
                Amount = billCandidate.Amount,
                DueDate = billCandidate.DueDate,
                Cadence = billCandidate.Cadence,
                Status = BillStatus.Upcoming,
                ReminderEnabled = true,
                SourceSmsId = smsId
            };
            var status = await _repository.SaveImportedBillAsync(CreateRaw(smsId, sender, body, receivedAt, bill.Name, bill.Amount), bill);
            if (status == SmsImportStatus.BillSaved) await _scheduler.ScheduleAsync(bill);
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
}
