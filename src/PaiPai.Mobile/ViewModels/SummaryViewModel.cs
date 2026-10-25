using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaiPai.Core;
using PaiPai.Data;

namespace PaiPai.Mobile.ViewModels;

public sealed record SummaryRow(string Label, decimal Amount, double Share);

public partial class SummaryViewModel : ObservableObject
{
    readonly LedgerRepository _repository;
    DateTime _month = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    public SummaryViewModel(LedgerRepository repository) => _repository = repository;
    public ObservableCollection<SummaryRow> Categories { get; } = new();
    public ObservableCollection<SummaryRow> Accounts { get; } = new();
    [ObservableProperty] private string _monthLabel = "";
    [ObservableProperty] private string _spent = "₹0";
    [ObservableProperty] private string _income = "₹0";
    [ObservableProperty] private string _net = "₹0";
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _hasNoCategories = true;
    [ObservableProperty] private bool _hasNoAccounts = true;

    [RelayCommand]
    async Task LoadAsync()
    {
        MonthLabel = _month.ToString("MMMM yyyy");
        var entries = await _repository.GetMonthAsync(_month.Year, _month.Month);
        var confirmed = entries.Where(x => x.Confirmed).ToList();
        var debit = confirmed.Where(x => x.Direction == Direction.Debit).ToList();
        var credit = confirmed.Where(x => x.Direction == Direction.Credit).ToList();
        var spentValue = debit.Sum(x => x.Amount);
        var incomeValue = credit.Sum(x => x.Amount);
        Spent = $"₹{spentValue:N2}";
        Income = $"₹{incomeValue:N2}";
        Net = $"₹{incomeValue - spentValue:N2}";

        Categories.Clear();
        foreach (var group in debit.GroupBy(x => string.IsNullOrWhiteSpace(x.Category) ? "Other" : x.Category)
                     .Select(g => new { g.Key, Amount = g.Sum(x => x.Amount) }).OrderByDescending(x => x.Amount))
            Categories.Add(new SummaryRow(group.Key, group.Amount, spentValue > 0 ? (double)(group.Amount / spentValue) : 0));

        Accounts.Clear();
        foreach (var group in debit.GroupBy(x => string.IsNullOrWhiteSpace(x.AccountHint)
                         ? $"{x.Method} - no account digits"
                         : $"{x.Method} - {x.AccountHint}")
                     .Select(g => new { g.Key, Amount = g.Sum(x => x.Amount) }).OrderByDescending(x => x.Amount))
            Accounts.Add(new SummaryRow(group.Key, group.Amount, spentValue > 0 ? (double)(group.Amount / spentValue) : 0));

        HasNoCategories = Categories.Count == 0;
        HasNoAccounts = Accounts.Count == 0;
        Status = debit.Count == 0 && credit.Count == 0 ? "No confirmed transactions for this month." : "";
    }

    [RelayCommand]
    async Task PreviousAsync()
    {
        _month = _month.AddMonths(-1);
        await LoadAsync();
    }

    [RelayCommand]
    async Task NextAsync()
    {
        _month = _month.AddMonths(1);
        await LoadAsync();
    }

    [RelayCommand]
    Task AddAsync() => Shell.Current.GoToAsync("add");
}
