using Android.App;
using Android.Content;
using Microsoft.Maui.ApplicationModel;
using PaiPai.Core;
using PaiPai.Data;
using PaiPai.Mobile.Services;

namespace PaiPai.Mobile.Platforms.Android;

public sealed class BillReminderScheduler : IBillReminderScheduler
{
    readonly LedgerRepository _repository;
    public BillReminderScheduler(LedgerRepository repository) => _repository = repository;

    public async Task<bool> HasPermissionAsync() =>
        await Permissions.CheckStatusAsync<NotificationPermission>() == PermissionStatus.Granted;

    public async Task<bool> RequestPermissionAsync() =>
        await Permissions.RequestAsync<NotificationPermission>() == PermissionStatus.Granted;

    public async Task ScheduleAsync(BillReminder bill)
    {
        await CancelAsync(bill.Id);
        if (!bill.ReminderEnabled || bill.Status != BillStatus.Upcoming || !await HasPermissionAsync()) return;
        if (bill.DueDate.Date < DateTime.Today) return;
        var trigger = bill.DueDate.Date.AddDays(-Math.Max(0, bill.ReminderDaysBefore)).AddHours(9);
        if (trigger <= DateTime.Now) trigger = DateTime.Now.AddMinutes(1);

        var context = global::Android.App.Application.Context;
        var intent = new Intent(context, typeof(BillReminderNotificationReceiver));
        intent.SetAction($"com.paipai.app.BILL_REMINDER.{bill.Id}");
        intent.PutExtra("billId", bill.Id);
        intent.PutExtra("billName", bill.Name);
        intent.PutExtra("billAmount", bill.Amount?.ToString("0.00") ?? "");
        intent.PutExtra("billDue", bill.DueDate.ToString("dd MMM yyyy"));
        var pending = PendingIntent.GetBroadcast(context, bill.Id, intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var manager = context.GetSystemService(Context.AlarmService) as AlarmManager;
        if (pending is not null)
            manager?.SetAndAllowWhileIdle(AlarmType.RtcWakeup,
                new DateTimeOffset(trigger).ToUnixTimeMilliseconds(), pending);
    }

    public Task CancelAsync(int billId)
    {
        var context = global::Android.App.Application.Context;
        (context.GetSystemService(Context.NotificationService) as NotificationManager)?.Cancel(billId);
        var intent = new Intent(context, typeof(BillReminderNotificationReceiver));
        intent.SetAction($"com.paipai.app.BILL_REMINDER.{billId}");
        var pending = PendingIntent.GetBroadcast(context, billId, intent,
            PendingIntentFlags.NoCreate | PendingIntentFlags.Immutable);
        if (pending is not null)
        {
            (context.GetSystemService(Context.AlarmService) as AlarmManager)?.Cancel(pending);
            pending.Cancel();
        }
        return Task.CompletedTask;
    }

    public async Task RescheduleAllAsync()
    {
        foreach (var bill in await _repository.GetBillsAsync()) await ScheduleAsync(bill);
    }
}
