using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaiPai.Core;
using PaiPai.Data;

namespace PaiPai.Mobile.ViewModels;

public partial class HistoryViewModel : ObservableObject
{
    readonly LedgerRepository _repository;
    public HistoryViewModel(LedgerRepository repository) => _repository = repository;

    public string[] DirectionFilters { get; } = ["All", "Expenses", "Income"];
    public string[] PeriodFilters { get; } = ["This month", "Last month", "All time"];
    public ObservableCollection<LedgerEntry> Entries { get; } = new();
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _directionFilter = "All";
    [ObservableProperty] private string _periodFilter = "This month";
    [ObservableProperty] private string _status = "";

    [RelayCommand]
    async Task LoadAsync()
    {
        var all = await _repository.GetEntriesAsync();
        IEnumerable<LedgerEntry> filtered = all;
        var today = DateTime.Today;
        if (PeriodFilter == "This month")
            filtered = filtered.Where(x => x.Date.Year == today.Year && x.Date.Month == today.Month);
        else if (PeriodFilter == "Last month")
        {
            var last = new DateTime(today.Year, today.Month, 1).AddMonths(-1);
            filtered = filtered.Where(x => x.Date.Year == last.Year && x.Date.Month == last.Month);
        }
        if (DirectionFilter == "Expenses") filtered = filtered.Where(x => x.Direction == Direction.Debit);
        else if (DirectionFilter == "Income") filtered = filtered.Where(x => x.Direction == Direction.Credit);

        var term = SearchText.Trim();
        if (!string.IsNullOrEmpty(term))
            filtered = filtered.Where(x => x.Merchant.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Category.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Method.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.AccountHint.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Reference.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Note.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.Amount.ToString("0.##").Contains(term, StringComparison.OrdinalIgnoreCase));

        Entries.Clear();
        foreach (var item in filtered) Entries.Add(item);
        Status = $"{Entries.Count:N0} transactions";
    }

    [RelayCommand] Task OpenDetailsAsync(LedgerEntry? entry) => entry is null
        ? Task.CompletedTask
        : Shell.Current.GoToAsync($"transaction-details?id={entry.Id}");

    [RelayCommand] Task AddAsync() => Shell.Current.GoToAsync("add");
}
