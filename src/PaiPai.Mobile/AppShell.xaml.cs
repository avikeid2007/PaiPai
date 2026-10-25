using PaiPai.Mobile.Pages;

namespace PaiPai.Mobile;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("add", typeof(AddExpensePage));
        Routing.RegisterRoute("import-sms", typeof(ImportSmsPage));
        Routing.RegisterRoute("review", typeof(SmsReviewPage));
        Routing.RegisterRoute("bill-edit", typeof(BillEditPage));
        Routing.RegisterRoute("transaction-details", typeof(TransactionDetailsPage));
    }
}
