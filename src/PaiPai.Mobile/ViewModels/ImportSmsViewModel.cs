using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaiPai.Data;
using PaiPai.Mobile.Services;

namespace PaiPai.Mobile.ViewModels;

public partial class ImportSmsViewModel : ObservableObject
{
    readonly ISmsInboxImporter _importer;
    readonly LedgerRepository _repository;
    CancellationTokenSource? _cancellation;

    public ImportSmsViewModel(ISmsInboxImporter importer, LedgerRepository repository)
    {
        _importer = importer;
        _repository = repository;
    }

    [ObservableProperty] private string _status = "Import SMS history from this device.";
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private string _buttonText = "Allow access and import";
    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private int _billCount;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isComplete;

    [RelayCommand]
    async Task LoadAsync()
    {
        var state = await _repository.GetImportStateAsync();
        PendingCount = await _repository.GetPendingReviewCountAsync();
        BillCount = (await _repository.GetBillsAsync()).Count;
        IsComplete = state.IsComplete;
        ButtonText = state.IsComplete ? "Inbox import complete" : state.ProcessedCount > 0 ? "Resume import" : "Allow access and import";
        if (state.IsComplete)
            Status = $"Import complete. {state.ProcessedCount:N0} inbox messages processed.";
        else if (state.ProcessedCount > 0)
            Status = $"Import paused after {state.ProcessedCount:N0} messages. Resume to continue.";
    }

    [RelayCommand]
    async Task ImportAsync()
    {
        if (IsBusy || IsComplete) return;
        IsBusy = true;
        _cancellation = new CancellationTokenSource();
        try
        {
            if (!await _importer.HasPermissionAsync() && !await _importer.RequestPermissionAsync())
            {
                Status = "SMS access was not granted. You can allow it in Android app permissions and try again.";
                return;
            }

            Status = "Reading inbox on this device…";
            var progress = new Progress<SmsImportProgress>(p =>
                ProgressText = $"Processed {p.ProcessedThisRun:N0} · added {p.Imported:N0} · review {p.NeedsReview:N0} · bills {p.Bills:N0} · ignored {p.Ignored:N0}");
            var result = await _importer.ImportAsync(progress, _cancellation.Token);
            IsComplete = result.IsComplete;
            PendingCount = await _repository.GetPendingReviewCountAsync();
            Status = result.IsComplete
                ? $"Import complete. {result.ProcessedThisRun:N0} messages processed in this run."
                : "Import finished.";
            ButtonText = result.IsComplete ? "Inbox import complete" : "Resume import";
            ProgressText = $"Added {result.Imported:N0} · review {result.NeedsReview:N0} · bills {result.Bills:N0} · ignored {result.Ignored:N0} · duplicates {result.Duplicates:N0}";
        }
        catch (OperationCanceledException)
        {
            Status = "Import paused. Tap resume to continue where it stopped.";
            ButtonText = "Resume import";
        }
        catch (Exception ex)
        {
            Status = $"Import stopped: {ex.Message}";
            ButtonText = "Resume import";
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            IsBusy = false;
            await LoadAsync();
        }
    }

    [RelayCommand]
    void Cancel()
    {
        if (_cancellation is null) return;
        Status = "Stopping safely after the current message…";
        _cancellation.Cancel();
    }

    [RelayCommand]
    Task OpenReviewAsync() => Shell.Current.GoToAsync("review");

    [RelayCommand]
    Task HomeAsync() => Shell.Current.GoToAsync("//home");
}
