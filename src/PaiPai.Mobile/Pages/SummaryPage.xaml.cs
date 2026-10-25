using PaiPai.Mobile.ViewModels;

namespace PaiPai.Mobile.Pages;

public partial class SummaryPage : ContentPage
{
    readonly SummaryViewModel _viewModel;
    public SummaryPage(SummaryViewModel viewModel)
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
