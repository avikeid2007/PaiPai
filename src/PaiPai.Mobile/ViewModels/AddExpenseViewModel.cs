using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaiPai.Core;
using PaiPai.Data;

namespace PaiPai.Mobile.ViewModels;

public partial class AddExpenseViewModel : ObservableObject
{
    readonly LedgerRepository _repo;
    public AddExpenseViewModel(LedgerRepository repo) => _repo = repo;

    public string[] Methods { get; } = ["Cash", "UPI", "Card", "Transfer", "Cheque", "ATM", "Other"];
    public string[] Directions { get; } = ["Expense", "Income"];
    public string[] Categories => Categorizer.Categories;

    [ObservableProperty] private int _entryId;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _saveLabel = "Save expense";
    [ObservableProperty] private string _direction = "Expense";
    [ObservableProperty] private string _method = "Cash";
    [ObservableProperty] private string _amountText = "";
    [ObservableProperty] private string _payee = "";
    [ObservableProperty] private string _category = "Food";
    [ObservableProperty] private string _accountHint = "";
    [ObservableProperty] private string _note = "";
    [ObservableProperty] private DateTime _date = DateTime.Today;
    [ObservableProperty] private string _errorMessage = "";

    public void Reset()
    {
        EntryId = 0;
        IsEditing = false;
        SaveLabel = "Save expense";
        Direction = "Expense";
        Method = "Cash";
        AmountText = "";
        Payee = "";
        Category = "Food";
        AccountHint = "";
        Note = "";
        Date = DateTime.Today;
        ErrorMessage = "";
    }

    public async Task LoadAsync(int id)
    {
        var entry = await _repo.GetEntryAsync(id);
        if (entry is null) return;
        EntryId = entry.Id;
        IsEditing = true;
        SaveLabel = "Save changes";
        Direction = entry.Direction == PaiPai.Core.Direction.Debit ? "Expense" : "Income";
        Method = string.IsNullOrWhiteSpace(entry.Method) ? "Other" : entry.Method;
        AmountText = entry.Amount.ToString("0.00", CultureInfo.InvariantCulture);
        Payee = entry.Merchant;
        Category = entry.Category;
        AccountHint = entry.AccountHint;
        Note = entry.Note;
        Date = entry.Date;
    }

    [RelayCommand]
    async Task SaveAsync()
    {
        ErrorMessage = "";
        var amountText = AmountText.Replace("₹", "", StringComparison.Ordinal).Replace(",", "", StringComparison.Ordinal).Trim();
        if (!decimal.TryParse(amountText, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount) || amount <= 0)
        {
            ErrorMessage = "Enter an amount greater than zero.";
            return;
        }

        var existing = EntryId > 0 ? await _repo.GetEntryAsync(EntryId) : null;
        var entry = existing ?? new LedgerEntry { Source = EntrySource.Manual };
        entry.Amount = amount;
        entry.Direction = Direction == "Income" ? PaiPai.Core.Direction.Credit : PaiPai.Core.Direction.Debit;
        entry.Date = Date.Date;
        entry.Merchant = string.IsNullOrWhiteSpace(Payee) ? "Unknown" : Payee.Trim();
        entry.Category = Category;
        entry.Method = Method;
        entry.AccountHint = AccountHint.Trim();
        entry.Note = Note.Trim();
        entry.Confirmed = true;
        if (existing is null)
            await _repo.AddAsync(entry);
        else
            await _repo.UpdateEntryAsync(entry);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    async Task DeleteAsync()
    {
        if (EntryId == 0) return;
        var page = Shell.Current.CurrentPage;
        var confirmed = await page.DisplayAlertAsync("Delete transaction", "Delete this transaction?", "Delete", "Keep");
        if (!confirmed) return;
        await _repo.DeleteEntryAsync(EntryId);
        await Shell.Current.GoToAsync("..");
    }
}
