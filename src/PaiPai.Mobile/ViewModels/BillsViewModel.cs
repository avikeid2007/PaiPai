using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaiPai.Core;
using PaiPai.Data;
using PaiPai.Mobile.Services;

namespace PaiPai.Mobile.ViewModels;

public partial class BillsViewModel : ObservableObject
{
    readonly LedgerRepository _repository;
    readonly IBillReminderScheduler _scheduler;
    public BillsViewModel(LedgerRepository repository, IBillReminderScheduler scheduler)
    {
        _repository = repository;
        _scheduler = scheduler;
    }

    public ObservableCollection<BillReminder> Bills { get; } = new();
    [ObservableProperty] private string _status = "";

    [RelayCommand]
    async Task LoadAsync()
    {
        Bills.Clear();
        foreach (var bill in await _repository.GetBillsAsync()) Bills.Add(bill);
        var overdue = Bills.Count(x => x.Status == BillStatus.Upcoming && x.DueDate.Date < DateTime.Today);
        Status = overdue > 0 ? $"{overdue} overdue - {Bills.Count:N0} open reminders" : $"{Bills.Count:N0} open reminders";
    }

    [RelayCommand] Task AddAsync() => Shell.Current.GoToAsync("bill-edit");

    [RelayCommand]
    Task EditAsync(BillReminder? bill) => bill is null
        ? Task.CompletedTask
        : Shell.Current.GoToAsync($"bill-edit?id={bill.Id}");

    [RelayCommand]
    async Task MarkPaidAsync(BillReminder? bill)
    {
        if (bill is null) return;
        bill.Status = BillStatus.Paid;
        await _repository.SaveBillAsync(bill);
        await _scheduler.CancelAsync(bill.Id);
        if (bill.Cadence != "One time")
        {
            var nextDue = bill.DueDate;
            do
                nextDue = bill.Cadence == "Yearly" ? nextDue.AddYears(1) : nextDue.AddMonths(1);
            while (nextDue.Date <= DateTime.Today);
            var next = new BillReminder
            {
                Name = bill.Name,
                Amount = bill.Amount,
                DueDate = nextDue,
                AccountHint = bill.AccountHint,
                Cadence = bill.Cadence,
                Status = BillStatus.Upcoming,
                ReminderEnabled = bill.ReminderEnabled,
                ReminderDaysBefore = bill.ReminderDaysBefore,
                Note = bill.Note
            };
            await _repository.SaveBillAsync(next);
            await _scheduler.ScheduleAsync(next);
        }
        await LoadAsync();
    }

    [RelayCommand]
    async Task DismissAsync(BillReminder? bill)
    {
        if (bill is null) return;
        bill.Status = BillStatus.Dismissed;
        await _repository.SaveBillAsync(bill);
        await _scheduler.CancelAsync(bill.Id);
        await LoadAsync();
    }
}
