using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaiPai.Core;
using PaiPai.Data;
using PaiPai.Mobile.Services;

namespace PaiPai.Mobile.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    readonly LedgerRepository _repo;
    readonly ISmsInboxImporter _importer;

    public HomeViewModel(LedgerRepository repo, ISmsInboxImporter importer)
    {
        _repo = repo;
        _importer = importer;
    }

    [ObservableProperty] private string _monthTotal = "₹0";
    [ObservableProperty] private int _pendingCount;
    [ObservableProperty] private int _upcomingBillCount;
    [ObservableProperty] private string _syncStatus = "";
    [ObservableProperty] private bool _hasNoRecent = true;
    public ObservableCollection<LedgerEntry> Recent { get; } = new();

    [RelayCommand]
    async Task LoadAsync()
    {
        try
        {
            if (await _importer.HasPermissionAsync())
            {
                var sync = await _importer.SyncNewAsync(null, CancellationToken.None);
                SyncStatus = sync.ProcessedThisRun > 0 ? $"Checked {sync.ProcessedThisRun:N0} recent or new messages." : "";
            }
        }
        catch (UnauthorizedAccessException)
        {
            SyncStatus = "SMS access is off. Enable it in Settings to sync new messages.";
        }

        var today = DateTime.Today;
        var items = await _repo.GetMonthAsync(today.Year, today.Month);
        MonthTotal = $"₹{items.Where(i => i.Direction == Direction.Debit && i.Confirmed).Sum(i => i.Amount):N0}";
        PendingCount = await _repo.GetPendingReviewCountAsync();
        UpcomingBillCount = (await _repo.GetBillsAsync()).Count(b => b.Status == BillStatus.Upcoming);
        Recent.Clear();
        foreach (var item in items.Take(8)) Recent.Add(item);
        HasNoRecent = Recent.Count == 0;
    }

    [RelayCommand] Task AddAsync() => Shell.Current.GoToAsync("add");
    [RelayCommand] Task ImportSmsAsync() => Shell.Current.GoToAsync("import-sms");
    [RelayCommand] Task ReviewSmsAsync() => Shell.Current.GoToAsync("review");
    [RelayCommand] Task HistoryAsync() => Shell.Current.GoToAsync("//history");
    [RelayCommand] Task SummaryAsync() => Shell.Current.GoToAsync("//summary");
    [RelayCommand] Task BillsAsync() => Shell.Current.GoToAsync("//bills");
    [RelayCommand] Task SettingsAsync() => Shell.Current.GoToAsync("//settings");
    [RelayCommand] Task OpenDetailsAsync(LedgerEntry? entry) => entry is null
        ? Task.CompletedTask
        : Shell.Current.GoToAsync($"transaction-details?id={entry.Id}");
}
