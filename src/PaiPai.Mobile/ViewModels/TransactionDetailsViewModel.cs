using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaiPai.Core;
using PaiPai.Data;

namespace PaiPai.Mobile.ViewModels;

public partial class TransactionDetailsViewModel : ObservableObject
{
    readonly LedgerRepository _repository;

    public TransactionDetailsViewModel(LedgerRepository repository) => _repository = repository;

    [ObservableProperty] private LedgerEntry? _entry;
    [ObservableProperty] private string _amountText = "";
    [ObservableProperty] private string _directionText = "";
    [ObservableProperty] private string _dateText = "";
    [ObservableProperty] private string _accountText = "";
    [ObservableProperty] private string _balanceText = "";
    [ObservableProperty] private string _referenceText = "";
    [ObservableProperty] private string _noteText = "";
    [ObservableProperty] private string _sourceText = "";
    [ObservableProperty] private string _confirmationText = "";
    [ObservableProperty] private string _status = "";

    public async Task LoadAsync(int id)
    {
        Entry = await _repository.GetEntryAsync(id);
        if (Entry is null)
        {
            Status = "This transaction could not be found.";
            return;
        }

        AmountText = $"₹{Entry.Amount:N2}";
        DirectionText = Entry.Direction == Direction.Debit ? "Expense" : "Income";
        DateText = Entry.Date.ToString("dddd, dd MMMM yyyy · h:mm tt");
        AccountText = !string.IsNullOrWhiteSpace(Entry.AccountName)
            ? (string.IsNullOrWhiteSpace(Entry.AccountHint) ? Entry.AccountName : $"{Entry.AccountName}, ending in {Entry.AccountHint}")
            : string.IsNullOrWhiteSpace(Entry.AccountHint) ? "Payment source: " + Entry.DisplayPaymentSource : $"Account or card ending in {Entry.AccountHint}";
        BalanceText = Entry.BalanceAfter is decimal balance ? $"Balance after transaction: ₹{balance:N2}" : "Balance after transaction: Not provided";
        ReferenceText = string.IsNullOrWhiteSpace(Entry.Reference) ? "Reference number: Not provided" : $"Reference number: {Entry.Reference}";
        NoteText = string.IsNullOrWhiteSpace(Entry.Note) ? "No note" : Entry.Note;
        SourceText = Entry.Source == EntrySource.Sms ? "Imported from SMS" : "Entered manually";
        ConfirmationText = Entry.Confirmed ? "Included in totals" : "Needs review";
        Status = "";
    }

    [RelayCommand]
    Task EditAsync() => Entry is null
        ? Task.CompletedTask
        : Shell.Current.GoToAsync($"add?id={Entry.Id}");

    [RelayCommand]
    async Task DeleteAsync()
    {
        if (Entry is null) return;
        if (!await Shell.Current.CurrentPage.DisplayAlertAsync("Delete transaction", "Delete this transaction?", "Delete", "Keep")) return;
        await _repository.DeleteEntryAsync(Entry.Id);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    Task HistoryAsync() => Shell.Current.GoToAsync("//history");

    [RelayCommand]
    Task HomeAsync() => Shell.Current.GoToAsync("//home");
}
