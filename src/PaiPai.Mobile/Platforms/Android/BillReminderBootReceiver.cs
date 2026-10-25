using Android.Content;
using Microsoft.Extensions.DependencyInjection;
using PaiPai.Mobile.Services;

namespace PaiPai.Mobile.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
[global::Android.App.IntentFilter(new[] { Intent.ActionBootCompleted })]
public sealed class BillReminderBootReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent?.Action != Intent.ActionBootCompleted) return;
        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                var scheduler = MauiProgram.Services.GetRequiredService<IBillReminderScheduler>();
                await scheduler.RescheduleAllAsync();
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Error("PaiPaiBills", ex.ToString());
            }
            finally
            {
                pending?.Finish();
            }
        });
    }
}
