using PaiPai.Data;
using PaiPai.Mobile.Pages;
using PaiPai.Mobile.Platforms.Android;
using PaiPai.Mobile.Services;
using PaiPai.Mobile.ViewModels;

namespace PaiPai.Mobile;

public static class MauiProgram
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static MauiApp CreateMauiApp()
    {
        var b = MauiApp.CreateBuilder();
        b.UseMauiApp<App>();
        b.Services.AddSingleton(new LedgerRepository(Path.Combine(FileSystem.AppDataDirectory, "paipai.db3")));
        b.Services.AddSingleton<ISmsInboxImporter, SmsInboxImporter>();
        b.Services.AddSingleton<SmsMessageProcessor>();
        b.Services.AddSingleton<IBillReminderScheduler, BillReminderScheduler>();
        b.Services.AddTransient<HomeViewModel>();
        b.Services.AddTransient<AddExpenseViewModel>();
        b.Services.AddTransient<ImportSmsViewModel>();
        b.Services.AddTransient<SmsReviewViewModel>();
        b.Services.AddTransient<HistoryViewModel>();
        b.Services.AddTransient<TransactionDetailsViewModel>();
        b.Services.AddTransient<SummaryViewModel>();
        b.Services.AddTransient<BillsViewModel>();
        b.Services.AddTransient<BillEditViewModel>();
        b.Services.AddTransient<SettingsViewModel>();
        b.Services.AddTransient<HomePage>();
        b.Services.AddTransient<AddExpensePage>();
        b.Services.AddTransient<ImportSmsPage>();
        b.Services.AddTransient<SmsReviewPage>();
        b.Services.AddTransient<HistoryPage>();
        b.Services.AddTransient<TransactionDetailsPage>();
        b.Services.AddTransient<SummaryPage>();
        b.Services.AddTransient<BillsPage>();
        b.Services.AddTransient<BillEditPage>();
        b.Services.AddTransient<SettingsPage>();
        var app = b.Build();
        Services = app.Services;
        return app;
    }
}
