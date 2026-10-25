using PaiPai.Mobile.ViewModels;

namespace PaiPai.Mobile.Pages;

public partial class TransactionDetailsPage : ContentPage, IQueryAttributable
{
    readonly TransactionDetailsViewModel _viewModel;

    public TransactionDetailsPage(TransactionDetailsViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && int.TryParse(Convert.ToString(value), out var id) && id > 0)
            _ = _viewModel.LoadAsync(id);
    }
}
