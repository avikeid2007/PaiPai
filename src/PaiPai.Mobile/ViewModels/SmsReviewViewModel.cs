using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PaiPai.Core;
using PaiPai.Data;

namespace PaiPai.Mobile.ViewModels;

public partial class SmsReviewViewModel : ObservableObject
{
    readonly LedgerRepository _repository;

    public SmsReviewViewModel(LedgerRepository repository) => _repository = repository;

    public ObservableCollection<RawSmsMessage> Messages { get; } = new();
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private int _messageCount;

    [RelayCommand]
    async Task LoadAsync()
    {
        Messages.Clear();
        foreach (var item in await _repository.GetReviewMessagesAsync()) Messages.Add(item);
        MessageCount = Messages.Count;
        Status = Messages.Count == 0 ? "Nothing needs review." : $"{Messages.Count:N0} messages need review.";
    }

    [RelayCommand]
    async Task ConfirmAsync(RawSmsMessage? message)
    {
        if (message is null) return;
        var saved = await _repository.ConfirmImportedSmsAsync(message.SmsId);
        if (!saved) Status = "This item could not be added because it duplicates an existing entry.";
        await LoadAsync();
    }

    [RelayCommand]
    async Task IgnoreAsync(RawSmsMessage? message)
    {
        if (message is null) return;
        await _repository.IgnoreImportedSmsAsync(message.SmsId);
        await LoadAsync();
    }

    [RelayCommand]
    Task AddManuallyAsync() => Shell.Current.GoToAsync("add");

    [RelayCommand]
    Task HomeAsync() => Shell.Current.GoToAsync("//home");

    [RelayCommand]
    Task HistoryAsync() => Shell.Current.GoToAsync("//history");
}
