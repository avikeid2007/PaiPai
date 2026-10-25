using PaiPai.Mobile.ViewModels;

namespace PaiPai.Mobile.Pages;

public partial class SettingsPage : ContentPage
{
    readonly SettingsViewModel _viewModel;
    public SettingsPage(SettingsViewModel viewModel)
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
