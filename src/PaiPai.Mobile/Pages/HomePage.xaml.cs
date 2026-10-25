using PaiPai.Mobile.ViewModels;

namespace PaiPai.Mobile.Pages;

public partial class HomePage : ContentPage
{
    readonly HomeViewModel _vm;

    public HomePage(HomeViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.LoadCommand.Execute(null);
    }
}
