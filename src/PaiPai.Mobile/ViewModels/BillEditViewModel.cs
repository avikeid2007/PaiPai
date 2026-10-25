using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaiPai.Core;
using PaiPai.Data;
using PaiPai.Mobile.Services;

namespace PaiPai.Mobile.ViewModels;

public partial class BillEditViewModel : ObservableObject
{
    readonly LedgerRepository _repository;
    readonly IBillReminderScheduler _scheduler;
    public BillEditViewModel(LedgerRepository repository, IBillReminderScheduler scheduler)
    {
        _repository = repository;
        _scheduler = scheduler;
    }

    public string[] Cadences { get; } = ["One time", "Monthly", "Yearly"];
    public int[] ReminderDayOptions { get; } = [0, 1, 3, 7];
    [ObservableProperty] private int _billId;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _amountText = "";
    [ObservableProperty] private DateTime _dueDate = DateTime.Today.AddDays(7);
    [ObservableProperty] private string _accountHint = "";
    [ObservableProperty] private string _cadence = "One time";
    [ObservableProperty] private bool _reminderEnabled = true;
    [ObservableProperty] private int _reminderDaysBefore = 1;
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private string _statusMessage = "";

    public void Reset()
    {
        BillId = 0;
        IsEditing = false;
        Name = "";
        AmountText = "";
        DueDate = DateTime.Today.AddDays(7);
        AccountHint = "";
        Cadence = "One time";
        ReminderEnabled = true;
        ReminderDaysBefore = 1;
        Note = "";
        StatusMessage = "";
    }

    public async Task LoadAsync(int id)
    {
        var bill = await _repository.GetBillAsync(id);
        if (bill is null) return;
        BillId = bill.Id;
        IsEditing = true;
        Name = bill.Name;
        AmountText = bill.Amount?.ToString("0.00", CultureInfo.InvariantCulture) ?? "";
        DueDate = bill.DueDate;
        AccountHint = bill.AccountHint;
        Cadence = bill.Cadence;
        ReminderEnabled = bill.ReminderEnabled;
        ReminderDaysBefore = bill.ReminderDaysBefore;
        Note = bill.Note;
    }

    [RelayCommand]
    async Task SaveAsync()
    {
        StatusMessage = "";
        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "Enter a bill or subscription name.";
            return;
        }

        decimal? amount = null;
        var text = AmountText.Replace("₹", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal).Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
            {
                StatusMessage = "Enter a valid amount, or leave it blank.";
                return;
            }
            amount = parsed;
        }

        var bill = BillId == 0 ? new BillReminder() : await _repository.GetBillAsync(BillId) ?? new BillReminder { Id = BillId };
        bill.Name = Name.Trim();
        bill.Amount = amount;
        bill.DueDate = DueDate.Date;
        bill.AccountHint = AccountHint.Trim();
        bill.Cadence = Cadence;
        bill.ReminderEnabled = ReminderEnabled;
        bill.ReminderDaysBefore = ReminderDaysBefore;
        bill.Note = Note.Trim();
        if (bill.Status == BillStatus.Dismissed) bill.Status = BillStatus.Upcoming;
        await _repository.SaveBillAsync(bill);

        if (bill.ReminderEnabled && !await _scheduler.HasPermissionAsync() && !await _scheduler.RequestPermissionAsync())
            await Shell.Current.CurrentPage.DisplayAlertAsync("Reminder saved", "Allow notifications in Settings to receive a bill reminder.", "OK");
        else
            await _scheduler.ScheduleAsync(bill);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    async Task DeleteAsync()
    {
        if (BillId == 0) return;
        var page = Shell.Current.CurrentPage;
        if (!await page.DisplayAlertAsync("Delete reminder", "Delete this bill reminder?", "Delete", "Keep")) return;
        await _repository.DeleteBillAsync(BillId);
        await _scheduler.CancelAsync(BillId);
        await Shell.Current.GoToAsync("..");
    }
}
