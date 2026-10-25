using PaiPai.Core;

namespace PaiPai.Mobile.Services;

public sealed record SmsImportProgress(int ProcessedThisRun, int Imported, int NeedsReview, int Bills, int Ignored, int Duplicates);
public sealed record SmsImportSummary(int ProcessedThisRun, int Imported, int NeedsReview, int Bills, int Ignored, int Duplicates, bool IsComplete);

public interface ISmsInboxImporter
{
    Task<bool> HasPermissionAsync();
    Task<bool> RequestPermissionAsync();
    Task<SmsImportSummary> ImportAsync(IProgress<SmsImportProgress>? progress, CancellationToken cancellationToken);
    Task<SmsImportSummary> SyncNewAsync(IProgress<SmsImportProgress>? progress, CancellationToken cancellationToken);
}
