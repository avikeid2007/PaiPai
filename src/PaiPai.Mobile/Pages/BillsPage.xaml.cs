using PaiPai.Mobile.ViewModels;

namespace PaiPai.Mobile.Pages;

public partial class BillsPage : ContentPage
{
    readonly BillsViewModel _viewModel;
    public BillsPage(BillsViewModel viewModel)
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
