using System.Globalization;
using PaiPai.Core;
using SQLite;

namespace PaiPai.Data;

public sealed class PersonalDataBackup
{
    public int SchemaVersion { get; set; } = 1;
    public DateTime ExportedAtUtc { get; set; } = DateTime.UtcNow;
    public List<LedgerEntry> Entries { get; set; } = [];
    public List<RawSmsMessage> RawMessages { get; set; } = [];
    public List<SmsImportState> ImportStates { get; set; } = [];
    public List<BillReminder> Bills { get; set; } = [];
    public List<AppSetting> Settings { get; set; } = [];
}

public sealed class LedgerRepository
{
    const string HighWatermarkSetting = "sms.inbox.high_watermark";
    readonly SQLiteAsyncConnection _db;
    readonly SemaphoreSlim _initializeLock = new(1, 1);
    bool _ready;

    public LedgerRepository(string path) => _db = new SQLiteAsyncConnection(path);

    async Task<SQLiteAsyncConnection> Db()
    {
        if (_ready) return _db;
        await _initializeLock.WaitAsync();
        try
        {
            if (!_ready)
            {
                await _db.CreateTableAsync<LedgerEntry>();
                await _db.CreateTableAsync<RawSmsMessage>();
                await _db.CreateTableAsync<SmsImportState>();
                await _db.CreateTableAsync<BillReminder>();
                await _db.CreateTableAsync<AppSetting>();
                await ConfirmPreviouslyParsedTransactionsAsync();
                _ready = true;
            }
        }
        finally { _initializeLock.Release(); }
        return _db;
    }

    async Task ConfirmPreviouslyParsedTransactionsAsync()
    {
        var pending = await _db.Table<RawSmsMessage>()
            .Where(x => x.Status == SmsImportStatus.AwaitingConfirmation)
            .ToListAsync();
        foreach (var raw in pending)
        {
            var entry = raw.LedgerEntryId is int entryId
                ? await _db.FindAsync<LedgerEntry>(entryId)
                : await _db.Table<LedgerEntry>().Where(x => x.SmsId == raw.SmsId).FirstOrDefaultAsync();
            if (entry is null)
            {
                raw.Status = SmsImportStatus.NeedsReview;
                raw.Reason = "A parsed transaction needs a fresh review.";
                await _db.UpdateAsync(raw);
                continue;
            }

            entry.Confirmed = true;
            await _db.UpdateAsync(entry);
            raw.LedgerEntryId = entry.Id;
            raw.Status = SmsImportStatus.Confirmed;
            raw.Reason = "Automatically added clear parsed transaction.";
            await _db.UpdateAsync(raw);
        }
    }

    public async Task<bool> AddAsync(LedgerEntry entry)
    {
        var db = await Db();
        if (entry.SmsId is long smsId && await db.Table<LedgerEntry>().Where(x => x.SmsId == smsId).CountAsync() > 0)
            return false;
        if (!string.IsNullOrEmpty(entry.Reference) &&
            await db.Table<LedgerEntry>().Where(x => x.Reference == entry.Reference && x.Amount == entry.Amount).CountAsync() > 0)
            return false;
        await db.InsertAsync(entry);
        return true;
    }

    public async Task UpdateEntryAsync(LedgerEntry entry)
    {
        var db = await Db();
        await db.UpdateAsync(entry);
    }

    public async Task DeleteEntryAsync(int id)
    {
        var db = await Db();
        var entry = await db.FindAsync<LedgerEntry>(id);
        if (entry is not null) await db.DeleteAsync(entry);
    }

    public async Task<LedgerEntry?> GetEntryAsync(int id)
    {
        var db = await Db();
        return await db.FindAsync<LedgerEntry>(id);
    }

    public async Task<List<LedgerEntry>> GetEntriesAsync()
    {
        var db = await Db();
        return await db.Table<LedgerEntry>().OrderByDescending(x => x.Date).ToListAsync();
    }

    public async Task<List<LedgerEntry>> GetMonthAsync(int year, int month)
    {
        var db = await Db();
        var start = new DateTime(year, month, 1);
        var end = start.AddMonths(1);
        return await db.Table<LedgerEntry>().Where(x => x.Date >= start && x.Date < end)
            .OrderByDescending(x => x.Date).ToListAsync();
    }

    public async Task<SmsImportState> GetImportStateAsync()
    {
        var db = await Db();
        var state = await db.FindAsync<SmsImportState>(1);
        if (state is not null) return state;
        state = new SmsImportState();
        await db.InsertAsync(state);
        return state;
    }

    public async Task<long?> GetInboxHighWatermarkAsync()
    {
        var value = await GetSettingAsync(HighWatermarkSetting);
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? id : null;
    }

    public async Task SetInboxHighWatermarkAsync(long smsId)
    {
        var current = await GetInboxHighWatermarkAsync();
        if (current is null || smsId > current.Value)
            await SetSettingAsync(HighWatermarkSetting, smsId.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>Stores relevant inbox text and a ledger entry when the parser finds a clear transaction.</summary>
    public async Task<SmsImportStatus> SaveImportedSmsAsync(RawSmsMessage raw, LedgerEntry? parsedEntry)
    {
        var db = await Db();
        var existingRaw = await db.FindAsync<RawSmsMessage>(raw.SmsId);
        if (existingRaw is not null) return existingRaw.Status;
        if (await HasMatchingRawMessageAsync(raw)) return SmsImportStatus.Duplicate;

        if (parsedEntry is not null)
        {
            parsedEntry.SmsId = raw.SmsId;
            var existingEntry = await db.Table<LedgerEntry>().Where(x => x.SmsId == raw.SmsId).FirstOrDefaultAsync();
            if (existingEntry is not null)
            {
                if (parsedEntry.Confirmed && !existingEntry.Confirmed)
                {
                    existingEntry.Confirmed = true;
                    await db.UpdateAsync(existingEntry);
                }
                raw.LedgerEntryId = existingEntry.Id;
                raw.Status = existingEntry.Confirmed ? SmsImportStatus.Confirmed : SmsImportStatus.AwaitingConfirmation;
            }
            else if (await AddAsync(parsedEntry))
            {
                raw.LedgerEntryId = parsedEntry.Id;
                raw.Status = parsedEntry.Confirmed ? SmsImportStatus.Confirmed : SmsImportStatus.AwaitingConfirmation;
            }
            else
            {
                raw.Status = SmsImportStatus.Duplicate;
                raw.Reason = "A matching ledger entry already exists";
            }
        }

        if (raw.Status != SmsImportStatus.Duplicate) await db.InsertAsync(raw);
        return raw.Status;
    }

    public async Task<bool> SaveBillReminderAsync(BillReminder bill)
    {
        var db = await Db();
        if (bill.SourceSmsId is long sourceId &&
            await db.Table<BillReminder>().Where(x => x.SourceSmsId == sourceId).CountAsync() > 0) return false;
        await db.InsertAsync(bill);
        return true;
    }

    public async Task<SmsImportStatus> SaveImportedBillAsync(RawSmsMessage raw, BillReminder bill)
    {
        var db = await Db();
        if (await db.FindAsync<RawSmsMessage>(raw.SmsId) is not null || await HasMatchingRawMessageAsync(raw))
            return SmsImportStatus.Duplicate;
        bill.SourceSmsId = raw.SmsId;
        if (!await SaveBillReminderAsync(bill)) return SmsImportStatus.Duplicate;
        raw.Status = SmsImportStatus.BillSaved;
        raw.Amount = bill.Amount;
        raw.Merchant = bill.Name;
        raw.Reason = "Bill reminder saved";
        await db.InsertAsync(raw);
        return raw.Status;
    }

    async Task<bool> HasMatchingRawMessageAsync(RawSmsMessage raw)
    {
        var db = await Db();
        var matches = await db.Table<RawSmsMessage>()
            .Where(x => x.Sender == raw.Sender && x.Body == raw.Body)
            .OrderByDescending(x => x.ReceivedAt)
            .Take(3)
            .ToListAsync();
        return matches.Any(x => Math.Abs((x.ReceivedAt - raw.ReceivedAt).TotalMinutes) <= 2);
    }

    public async Task CheckpointSmsImportAsync(long? lastSmsId, bool isComplete = false)
    {
        var db = await Db();
        var state = await GetImportStateAsync();
        if (lastSmsId is long id)
        {
            state.LastProcessedSmsId = id;
            state.ProcessedCount++;
            await SetInboxHighWatermarkAsync(id);
        }
        state.IsComplete = isComplete;
        state.LastRunAtUtc = DateTime.UtcNow;
        await db.UpdateAsync(state);
    }

    public async Task<List<RawSmsMessage>> GetReviewMessagesAsync()
    {
        var db = await Db();
        return await db.Table<RawSmsMessage>()
            .Where(x => x.Status == SmsImportStatus.NeedsReview || x.Status == SmsImportStatus.AwaitingConfirmation)
            .OrderByDescending(x => x.ReceivedAt).ToListAsync();
    }

    public async Task<int> GetPendingReviewCountAsync()
    {
        var db = await Db();
        return await db.Table<RawSmsMessage>()
            .Where(x => x.Status == SmsImportStatus.NeedsReview || x.Status == SmsImportStatus.AwaitingConfirmation)
            .CountAsync();
    }

    public async Task<bool> ConfirmImportedSmsAsync(long smsId)
    {
        var db = await Db();
        var raw = await db.FindAsync<RawSmsMessage>(smsId);
        if (raw is null || raw.Amount is null || raw.Direction is null) return false;

        LedgerEntry? entry = raw.LedgerEntryId is int entryId
            ? await db.FindAsync<LedgerEntry>(entryId)
            : await db.Table<LedgerEntry>().Where(x => x.SmsId == smsId).FirstOrDefaultAsync();
        if (entry is null)
        {
            entry = new LedgerEntry
            {
                Amount = raw.Amount.Value,
                Direction = raw.Direction.Value,
                Date = raw.ReceivedAt,
                Merchant = string.IsNullOrWhiteSpace(raw.Merchant) ? "Unknown" : raw.Merchant,
                Category = Categorizer.Categorize(raw.Merchant),
                Method = raw.Method,
                AccountHint = raw.AccountHint,
                BalanceAfter = raw.BalanceAfter,
                Reference = raw.Reference,
                Source = EntrySource.Sms,
                SmsId = raw.SmsId,
                Confirmed = true
            };
            if (!await AddAsync(entry))
            {
                raw.Status = SmsImportStatus.Duplicate;
                raw.Reason = "A matching ledger entry already exists";
                await db.UpdateAsync(raw);
                return false;
            }
            raw.LedgerEntryId = entry.Id;
        }
        else
        {
            entry.Confirmed = true;
            await db.UpdateAsync(entry);
        }

        raw.Status = SmsImportStatus.Confirmed;
        raw.Reason = "Confirmed by user";
        await db.UpdateAsync(raw);
        return true;
    }

    public async Task IgnoreImportedSmsAsync(long smsId)
    {
        var db = await Db();
        var raw = await db.FindAsync<RawSmsMessage>(smsId);
        if (raw is null) return;
        if (raw.LedgerEntryId is int entryId)
        {
            var entry = await db.FindAsync<LedgerEntry>(entryId);
            if (entry is not null && !entry.Confirmed) await db.DeleteAsync(entry);
        }
        raw.Status = SmsImportStatus.Ignored;
        raw.Reason = "Ignored by user";
        raw.LedgerEntryId = null;
        await db.UpdateAsync(raw);
    }

    public async Task<List<BillReminder>> GetBillsAsync(bool includeDismissed = false)
    {
        var db = await Db();
        return includeDismissed
            ? await db.Table<BillReminder>().OrderBy(x => x.DueDate).ToListAsync()
            : await db.Table<BillReminder>().Where(x => x.Status != BillStatus.Dismissed).OrderBy(x => x.DueDate).ToListAsync();
    }

    public async Task<BillReminder?> GetBillAsync(int id)
    {
        var db = await Db();
        return await db.FindAsync<BillReminder>(id);
    }

    public async Task SaveBillAsync(BillReminder bill)
    {
        var db = await Db();
        if (bill.Id == 0) await db.InsertAsync(bill);
        else await db.UpdateAsync(bill);
    }

    public async Task SetBillStatusAsync(int id, BillStatus status)
    {
        var db = await Db();
        var bill = await db.FindAsync<BillReminder>(id);
        if (bill is null) return;
        bill.Status = status;
        await db.UpdateAsync(bill);
    }

    public async Task DeleteBillAsync(int id)
    {
        var db = await Db();
        var bill = await db.FindAsync<BillReminder>(id);
        if (bill is not null) await db.DeleteAsync(bill);
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        var db = await Db();
        return (await db.FindAsync<AppSetting>(key))?.Value;
    }

    public async Task SetSettingAsync(string key, string value)
    {
        var db = await Db();
        var setting = await db.FindAsync<AppSetting>(key);
        if (setting is null) await db.InsertAsync(new AppSetting { Key = key, Value = value });
        else { setting.Value = value; await db.UpdateAsync(setting); }
    }

    public async Task<PersonalDataBackup> CreateBackupAsync()
    {
        var db = await Db();
        return new PersonalDataBackup
        {
            Entries = await db.Table<LedgerEntry>().ToListAsync(),
            RawMessages = await db.Table<RawSmsMessage>().ToListAsync(),
            ImportStates = await db.Table<SmsImportState>().ToListAsync(),
            Bills = await db.Table<BillReminder>().ToListAsync(),
            Settings = await db.Table<AppSetting>().ToListAsync()
        };
    }

    public async Task RestoreBackupAsync(PersonalDataBackup backup)
    {
        if (backup.SchemaVersion != 1) throw new InvalidDataException("This backup version is not supported.");
        var db = await Db();
        await _db.RunInTransactionAsync(connection =>
        {
            connection.DeleteAll<LedgerEntry>();
            connection.DeleteAll<RawSmsMessage>();
            connection.DeleteAll<SmsImportState>();
            connection.DeleteAll<BillReminder>();
            connection.DeleteAll<AppSetting>();
            if (backup.Entries.Count > 0) connection.InsertAll(backup.Entries);
            if (backup.RawMessages.Count > 0) connection.InsertAll(backup.RawMessages);
            if (backup.ImportStates.Count > 0) connection.InsertAll(backup.ImportStates);
            if (backup.Bills.Count > 0) connection.InsertAll(backup.Bills);
            if (backup.Settings.Count > 0) connection.InsertAll(backup.Settings);
        });
    }

    public async Task ClearAllDataAsync()
    {
        _ = await Db();
        await _db.RunInTransactionAsync(connection =>
        {
            connection.DeleteAll<LedgerEntry>();
            connection.DeleteAll<RawSmsMessage>();
            connection.DeleteAll<SmsImportState>();
            connection.DeleteAll<BillReminder>();
            connection.DeleteAll<AppSetting>();
        });
    }
}
