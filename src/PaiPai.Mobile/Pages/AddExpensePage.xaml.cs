using PaiPai.Mobile.ViewModels;

namespace PaiPai.Mobile.Pages;

public partial class AddExpensePage : ContentPage, IQueryAttributable
{
    readonly AddExpenseViewModel _vm;

    public AddExpensePage(AddExpenseViewModel vm)
    {
        InitializeComponent();
        BindingContext = _vm = vm;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && int.TryParse(Convert.ToString(value), out var id) && id > 0)
            _ = _vm.LoadAsync(id);
        else
            _vm.Reset();
    }
}
