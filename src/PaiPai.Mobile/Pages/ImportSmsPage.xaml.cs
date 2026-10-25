using PaiPai.Mobile.ViewModels;

namespace PaiPai.Mobile.Pages;

public partial class ImportSmsPage : ContentPage
{
    readonly ImportSmsViewModel _viewModel;

    public ImportSmsPage(ImportSmsViewModel viewModel)
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
