using System.Globalization;
using System.Text;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using PaiPai.Data;
using PaiPai.Mobile.Platforms.Android;
using PaiPai.Mobile.Services;

namespace PaiPai.Mobile.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    readonly LedgerRepository _repository;
    readonly ISmsInboxImporter _importer;
    readonly IBillReminderScheduler _scheduler;
    public SettingsViewModel(LedgerRepository repository, ISmsInboxImporter importer, IBillReminderScheduler scheduler)
    {
        _repository = repository;
        _importer = importer;
        _scheduler = scheduler;
    }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(SmsReadPermissionLabel))]
    private bool _hasSmsReadPermission;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(LiveSmsPermissionLabel))]
    private bool _hasLiveSmsPermission;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(NotificationPermissionLabel))]
    private bool _hasNotificationPermission;
    [ObservableProperty] private string _status = "";

    public string SmsReadPermissionLabel => HasSmsReadPermission ? "On" : "Off";
    public string LiveSmsPermissionLabel => HasLiveSmsPermission ? "On" : "Off";
    public string NotificationPermissionLabel => HasNotificationPermission ? "On" : "Off";

    [RelayCommand]
    async Task LoadAsync()
    {
        HasSmsReadPermission = await _importer.HasPermissionAsync();
        HasLiveSmsPermission = await Permissions.CheckStatusAsync<SmsReceivePermission>() == PermissionStatus.Granted;
        HasNotificationPermission = await _scheduler.HasPermissionAsync();
    }

    [RelayCommand]
    Task OpenImportAsync() => Shell.Current.GoToAsync("import-sms");

    [RelayCommand]
    async Task EnableLiveSmsAsync()
    {
        HasLiveSmsPermission = await Permissions.RequestAsync<SmsReceivePermission>() == PermissionStatus.Granted;
        Status = HasLiveSmsPermission
            ? "Live SMS detection is enabled. Clear matches are added automatically; uncertain activity stays in Review."
            : "SMS receive permission was not granted. You can change it in Android app settings.";
    }

    [RelayCommand]
    async Task EnableNotificationsAsync()
    {
        HasNotificationPermission = await _scheduler.RequestPermissionAsync();
        if (HasNotificationPermission)
        {
            await _scheduler.RescheduleAllAsync();
            Status = "Bill reminders are scheduled where their due dates allow.";
        }
        else Status = "Notifications were not allowed. Bills remain visible in the app.";
    }

    [RelayCommand]
    async Task ExportCsvAsync()
    {
        var entries = await _repository.GetEntriesAsync();
        var path = Path.Combine(FileSystem.CacheDirectory, $"paipai-transactions-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        await using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
        {
            await writer.WriteLineAsync("Date,Direction,Amount,Merchant,Category,Method,Account,Source,Reference,Note,Confirmed");
            foreach (var e in entries)
                await writer.WriteLineAsync(string.Join(",", new[]
                {
                    Csv(e.Date.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)),
                    Csv(e.Direction.ToString()), Csv(e.Amount.ToString("0.00", CultureInfo.InvariantCulture)),
                    Csv(e.Merchant), Csv(e.Category), Csv(e.Method), Csv(e.AccountHint), Csv(e.Source.ToString()),
                    Csv(e.Reference), Csv(e.Note), Csv(e.Confirmed.ToString())
                }));
        }
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Export Pai Pai transactions",
            File = new ShareFile(path, "text/csv")
        });
        Status = $"Exported {entries.Count:N0} transactions.";
    }

    [RelayCommand]
    async Task BackupAsync()
    {
        var backup = await _repository.CreateBackupAsync();
        var path = Path.Combine(FileSystem.CacheDirectory, $"paipai-backup-{DateTime.Now:yyyyMMdd-HHmm}.json");
        var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, new UTF8Encoding(false));
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Back up Pai Pai data",
            File = new ShareFile(path, "application/json")
        });
        Status = "Backup created. It contains stored financial SMS text; keep the file private.";
    }

    [RelayCommand]
    async Task RestoreBackupAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Choose a Pai Pai backup" });
        if (file is null) return;
        var page = Shell.Current.CurrentPage;
        if (!await page.DisplayAlertAsync("Replace app data?", "Restoring replaces transactions, bills and locally stored financial messages with the backup file.", "Restore", "Cancel")) return;
        await using var stream = await file.OpenReadAsync();
        var backup = await JsonSerializer.DeserializeAsync<PersonalDataBackup>(stream);
        if (backup is null) throw new InvalidDataException("The selected file is not a valid Pai Pai backup.");
        foreach (var bill in await _repository.GetBillsAsync(includeDismissed: true))
            await _scheduler.CancelAsync(bill.Id);
        await _repository.RestoreBackupAsync(backup);
        await _scheduler.RescheduleAllAsync();
        Status = $"Restored {backup.Entries.Count:N0} transactions and {backup.Bills.Count:N0} bill reminders.";
    }

    [RelayCommand]
    async Task ClearAllDataAsync()
    {
        var page = Shell.Current.CurrentPage;
        if (!await page.DisplayAlertAsync("Clear all app data?", "This deletes transactions, bill reminders and stored financial SMS review text from this device.", "Clear all", "Cancel")) return;
        foreach (var bill in await _repository.GetBillsAsync(includeDismissed: true))
            await _scheduler.CancelAsync(bill.Id);
        await _repository.ClearAllDataAsync();
        Status = "All Pai Pai data was deleted from this device.";
    }

    static string Csv(string value) => $"\"{value.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";
}
