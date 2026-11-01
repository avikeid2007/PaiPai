using System.Security.Cryptography;
using System.Text;
using Android.Content;
using Android.Provider;
using Microsoft.Extensions.DependencyInjection;
using PaiPai.Mobile.Services;

namespace PaiPai.Mobile.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = true)]
[global::Android.App.IntentFilter(new[] { "android.provider.Telephony.SMS_RECEIVED" })]
public sealed class IncomingSmsReceiver : BroadcastReceiver
{
    public override void OnReceive(Context? context, Intent? intent)
    {
        if (context is null || intent?.Action != "android.provider.Telephony.SMS_RECEIVED") return;
        // Unpack the broadcast before leaving the receiver callback. Keep only plain message data
        // in the async work so Android does not have to retain the Intent while the task runs.
        var messages = Telephony.Sms.Intents.GetMessagesFromIntent(intent);
        if (messages is null || messages.Length == 0) return;
        var grouped = messages.GroupBy(message => (message.OriginatingAddress ?? "", message.TimestampMillis))
            .Select(group => new
            {
                Sender = group.Key.Item1,
                Body = string.Concat(group.Select(message => message.MessageBody ?? "")),
                Timestamp = group.Key.Item2
            })
            .ToArray();
        global::Android.Util.Log.Info("PaiPaiSms", $"SMS broadcast received with {grouped.Length} message(s).");
        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                var processor = MauiProgram.Services.GetRequiredService<SmsMessageProcessor>();
                foreach (var message in grouped)
                {
                    var receivedAt = DateTimeOffset.FromUnixTimeMilliseconds(message.Timestamp).LocalDateTime;
                    var status = await processor.ProcessAsync(CreateStableId(message.Sender, message.Timestamp, message.Body), message.Sender, message.Body, receivedAt);
                    global::Android.Util.Log.Info("PaiPaiSms", $"Incoming SMS processed with status {status}.");
                }
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Error("PaiPaiSms", ex.ToString());
            }
            finally
            {
                pending?.Finish();
            }
        });
    }

    static long CreateStableId(string sender, long timestamp, string body)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{sender}|{timestamp}|{body}"));
        var positive = BitConverter.ToInt64(bytes, 0) & long.MaxValue;
        return -Math.Max(positive, 1);
    }
}
