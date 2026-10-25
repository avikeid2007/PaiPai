using PaiPai.Mobile.ViewModels;

namespace PaiPai.Mobile.Pages;

public partial class HistoryPage : ContentPage
{
    readonly HistoryViewModel _viewModel;
    public HistoryPage(HistoryViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.LoadCommand.Execute(null);
    }
}
