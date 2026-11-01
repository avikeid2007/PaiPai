using SQLite;

namespace PaiPai.Core;

public enum Direction { Debit, Credit }
public enum EntrySource { Sms, Manual }
public enum SmsImportStatus { Ignored, NeedsReview, AwaitingConfirmation, Confirmed, Duplicate, BillSaved }
public enum BillStatus { Upcoming, Paid, Dismissed }

public class LedgerEntry
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public decimal Amount { get; set; }
    public Direction Direction { get; set; }
    public DateTime Date { get; set; }
    public string Merchant { get; set; } = "";
    public string PayeeNickname { get; set; } = "";
    public string Category { get; set; } = "Other";
    public string Method { get; set; } = "";       // UPI, Card, Cash, Transfer, Cheque, ATM, Other
    public string AccountHint { get; set; } = "";  // last digits only
    public string AccountName { get; set; } = "";
    public decimal? BalanceAfter { get; set; }
    public string Reference { get; set; } = "";
    public EntrySource Source { get; set; }
    public long? SmsId { get; set; }
    public string Note { get; set; } = "";
    public bool Confirmed { get; set; }
    [Ignore] public string DisplayCategory => Confirmed ? Category : $"{Category} - pending";
    [Ignore] public string DisplayDateMethod => $"{Date:dd MMM yyyy} - {Method}";
    [Ignore] public string DisplayMerchant => string.IsNullOrWhiteSpace(PayeeNickname) ? Merchant : PayeeNickname;
    [Ignore] public string DisplayPaymentSource => !string.IsNullOrWhiteSpace(AccountName)
        ? (string.IsNullOrWhiteSpace(AccountHint) ? AccountName : $"{AccountName} ··{AccountHint}")
        : !string.IsNullOrWhiteSpace(AccountHint) ? $"{Method} ··{AccountHint}"
        : string.IsNullOrWhiteSpace(Method) ? "Payment source not set" : Method;
}

/// <summary>Locally stored inbox message and parser result, keyed by the Android SMS row id.</summary>
public class RawSmsMessage
{
    [PrimaryKey] public long SmsId { get; set; }
    public string Sender { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime ReceivedAt { get; set; }
    public SmsImportStatus Status { get; set; }
    public string Reason { get; set; } = "";
    public decimal? Amount { get; set; }
    public Direction? Direction { get; set; }
    public string Merchant { get; set; } = "";
    public string Method { get; set; } = "";
    public string AccountHint { get; set; } = "";
    public string AccountName { get; set; } = "";
    public decimal? BalanceAfter { get; set; }
    public string Reference { get; set; } = "";
    public int? LedgerEntryId { get; set; }
    [Ignore] public bool HasCandidate => Amount.HasValue && Direction.HasValue;
}

/// <summary>Checkpoint for a resumable, one-time inbox scan.</summary>
public class SmsImportState
{
    [PrimaryKey] public int Id { get; set; } = 1;
    public long? LastProcessedSmsId { get; set; }
    public bool IsComplete { get; set; }
    public int ProcessedCount { get; set; }
    public DateTime? LastRunAtUtc { get; set; }
}

public class BillReminder
{
    [PrimaryKey, AutoIncrement] public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal? Amount { get; set; }
    public DateTime DueDate { get; set; }
    public string AccountHint { get; set; } = "";
    public string BillingPeriod { get; set; } = "";
    public DateTime? DisconnectionDate { get; set; }
    public string Category { get; set; } = "Other";
    public string Cadence { get; set; } = "One time";
    public BillStatus Status { get; set; } = BillStatus.Upcoming;
    public bool ReminderEnabled { get; set; } = true;
    public int ReminderDaysBefore { get; set; } = 1;
    public string Note { get; set; } = "";
    public long? SourceSmsId { get; set; }
    [Ignore] public string DisplayDueDate => $"Due {DueDate:dd MMM yyyy} - {Cadence}";
    [Ignore] public string DisplayAmount => Amount.HasValue ? $"₹{Amount.Value:N2}" : "Amount not set";
    [Ignore] public string DisplayStatus => Status == BillStatus.Upcoming && DueDate.Date < DateTime.Today ? "Overdue" : Status.ToString();
    [Ignore] public string DisplayAccountHint => string.IsNullOrWhiteSpace(AccountHint) ? "" : $"Account/card - {AccountHint}";
    [Ignore] public string DisplayBillingPeriod => string.IsNullOrWhiteSpace(BillingPeriod) ? "" : $"Billing period - {BillingPeriod}";
    [Ignore] public string DisplayDisconnectionDate => DisconnectionDate.HasValue ? $"Disconnection date - {DisconnectionDate.Value:dd MMM yyyy}" : "";
    [Ignore] public bool CanMarkPaid => Status == BillStatus.Upcoming;
    [Ignore] public bool CanDismiss => Status == BillStatus.Upcoming;
}

public class AppSetting
{
    [PrimaryKey] public string Key { get; set; } = "";
    public string Value { get; set; } = "";
}

public sealed record ParsedBillReminder(string Name, decimal? Amount, DateTime DueDate, string Cadence,
    string AccountHint = "", string BillingPeriod = "", DateTime? DisconnectionDate = null,
    bool IsPaymentConfirmation = false, string Category = "Other");
