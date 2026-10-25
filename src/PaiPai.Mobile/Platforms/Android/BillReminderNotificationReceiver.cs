using Android.App;
using Android.Content;
using Android.OS;
using System.Runtime.Versioning;

namespace PaiPai.Mobile.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public sealed class BillReminderNotificationReceiver : BroadcastReceiver
{
    const string ChannelId = "bill-reminders";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent is null) return;
        var manager = context.GetSystemService(Context.NotificationService) as NotificationManager;
        if (manager is null) return;

        var billName = intent.GetStringExtra("billName") ?? "Bill reminder";
        var amount = intent.GetStringExtra("billAmount");
        var due = intent.GetStringExtra("billDue");
        var message = string.IsNullOrWhiteSpace(amount) ? $"Due {due}" : $"₹{amount} due {due}";
        Notification.Builder notification;
        if (OperatingSystem.IsAndroidVersionAtLeast(26))
            notification = CreateModernBuilder(context, manager);
        else
            notification = CreateLegacyBuilder(context);
        notification.SetSmallIcon(global::Android.Resource.Drawable.IcDialogInfo)
            .SetContentTitle($"Upcoming bill: {billName}")
            .SetContentText(message)
            .SetAutoCancel(true);
        manager.Notify(intent.GetIntExtra("billId", 0), notification.Build());
    }

    [SupportedOSPlatform("android26.0")]
    static Notification.Builder CreateModernBuilder(Context context, NotificationManager manager)
    {
        if (manager.GetNotificationChannel(ChannelId) is null)
            manager.CreateNotificationChannel(new NotificationChannel(ChannelId, "Bill reminders", NotificationImportance.Default));
        return new Notification.Builder(context, ChannelId);
    }

    static Notification.Builder CreateLegacyBuilder(Context context)
    {
#pragma warning disable CA1422 // This builder is selected only before API 26, when channel-based notifications are unavailable.
        return new Notification.Builder(context);
#pragma warning restore CA1422
    }
}
