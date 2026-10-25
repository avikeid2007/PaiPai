using PaiPai.Mobile.ViewModels;

namespace PaiPai.Mobile.Pages;

public partial class BillEditPage : ContentPage, IQueryAttributable
{
    readonly BillEditViewModel _viewModel;
    public BillEditPage(BillEditViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && int.TryParse(Convert.ToString(value), out var id) && id > 0)
            _ = _viewModel.LoadAsync(id);
        else
            _viewModel.Reset();
    }
}
