using PaiPai.Mobile.ViewModels;

namespace PaiPai.Mobile.Pages;

public partial class SmsReviewPage : ContentPage
{
    readonly SmsReviewViewModel _viewModel;

    public SmsReviewPage(SmsReviewViewModel viewModel)
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
