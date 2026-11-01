using Android.Provider;
using Microsoft.Maui.ApplicationModel;
using PaiPai.Core;
using PaiPai.Data;
using PaiPai.Mobile.Services;

namespace PaiPai.Mobile.Platforms.Android;

public sealed class SmsInboxImporter : ISmsInboxImporter
{
    static readonly string[] Projection = ["_id", "address", "body", "date"];
    readonly LedgerRepository _repository;
    readonly SmsMessageProcessor _processor;

    public SmsInboxImporter(LedgerRepository repository, SmsMessageProcessor processor)
    {
        _repository = repository;
        _processor = processor;
    }

    public async Task<bool> HasPermissionAsync() =>
        await Permissions.CheckStatusAsync<SmsInboxPermission>() == PermissionStatus.Granted;

    public async Task<bool> RequestPermissionAsync() =>
        await Permissions.RequestAsync<SmsInboxPermission>() == PermissionStatus.Granted;

    public async Task<SmsImportSummary> ImportAsync(
        IProgress<SmsImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync()) throw new UnauthorizedAccessException("SMS inbox permission has not been granted.");
        var state = await _repository.GetImportStateAsync();
        if (state.IsComplete) return new(0, 0, 0, 0, 0, 0, true);

        var selection = state.LastProcessedSmsId is long id ? "_id < ?" : null;
        string[]? args = state.LastProcessedSmsId is long last ? [last.ToString()] : null;
        var counts = await ReadRowsAsync(selection, args, "_id DESC", progress, cancellationToken);
        await _repository.CheckpointSmsImportAsync(null, isComplete: true);
        return new(counts.Processed, counts.Imported, counts.Review, counts.Bills, counts.Ignored, counts.Duplicates, true);
    }

    public async Task<SmsImportSummary> SyncNewAsync(
        IProgress<SmsImportProgress>? progress,
        CancellationToken cancellationToken)
    {
        if (!await HasPermissionAsync()) throw new UnauthorizedAccessException("SMS inbox permission has not been granted.");
        var state = await _repository.GetImportStateAsync();
        var watermark = await _repository.GetInboxHighWatermarkAsync();
        string? selection;
        string[]? args;
        string sortOrder;
        if (watermark is long high)
        {
            selection = "_id > ?";
            args = [high.ToString()];
            sortOrder = "_id ASC";
        }
        else
        {
            // Live detection can be enabled before a full 10k+ message import. If Android
            // misses a broadcast while the app is force-stopped, catch up a bounded recent
            // window on the next launch instead of scanning the entire inbox.
            var recentCutoff = DateTimeOffset.Now.AddDays(-3).ToUnixTimeMilliseconds();
            selection = "date >= ?";
            args = [recentCutoff.ToString()];
            sortOrder = "date ASC";
        }
        var recentWindow = watermark is null;
        var counts = await ReadRowsAsync(selection, args, sortOrder, progress, cancellationToken, recentWindow ? 250 : int.MaxValue);
        await _repository.CheckpointSmsImportAsync(null, isComplete: state.IsComplete);
        return new(counts.Processed, counts.Imported, counts.Review, counts.Bills, counts.Ignored, counts.Duplicates, state.IsComplete);
    }

    async Task<ImportCounts> ReadRowsAsync(
        string? selection,
        string[]? selectionArgs,
        string sortOrder,
        IProgress<SmsImportProgress>? progress,
        CancellationToken cancellationToken,
        int maxRows = int.MaxValue)
    {
        var resolver = global::Android.App.Application.Context.ContentResolver
            ?? throw new InvalidOperationException("Android SMS content resolver is unavailable.");
        var counts = new ImportCounts();
        await Task.Run(async () =>
        {
            using var cursor = resolver.Query(Telephony.Sms.Inbox.ContentUri!, Projection, selection, selectionArgs, sortOrder);
            if (cursor is null) return;

            var idIndex = cursor.GetColumnIndexOrThrow("_id");
            var addressIndex = cursor.GetColumnIndexOrThrow("address");
            var bodyIndex = cursor.GetColumnIndexOrThrow("body");
            var dateIndex = cursor.GetColumnIndexOrThrow("date");
            while (counts.Processed < maxRows && cursor.MoveToNext())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var id = cursor.GetLong(idIndex);
                var body = cursor.GetString(bodyIndex) ?? "";
                var sender = cursor.GetString(addressIndex) ?? "";
                var receivedAt = DateTimeOffset.FromUnixTimeMilliseconds(cursor.GetLong(dateIndex)).LocalDateTime;
                var status = await _processor.ProcessAsync(id, sender, body, receivedAt);
                await _repository.CheckpointSmsImportAsync(id, isComplete: false);
                counts.Processed++;
                counts.Add(status);
                progress?.Report(new(counts.Processed, counts.Imported, counts.Review, counts.Bills, counts.Ignored, counts.Duplicates));
            }
        }, cancellationToken);
        return counts;
    }

    sealed class ImportCounts
    {
        public int Processed, Imported, Review, Bills, Ignored, Duplicates;
        public void Add(SmsImportStatus status)
        {
            switch (status)
            {
                case SmsImportStatus.AwaitingConfirmation:
                case SmsImportStatus.Confirmed: Imported++; break;
                case SmsImportStatus.NeedsReview: Review++; break;
                case SmsImportStatus.BillSaved: Bills++; break;
                case SmsImportStatus.Duplicate: Duplicates++; break;
                default: Ignored++; break;
            }
        }
    }
}
