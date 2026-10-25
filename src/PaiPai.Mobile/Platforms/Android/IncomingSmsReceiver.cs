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
        var pending = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
                var messages = Telephony.Sms.Intents.GetMessagesFromIntent(intent);
                if (messages is null || messages.Length == 0) return;
                var processor = MauiProgram.Services.GetRequiredService<SmsMessageProcessor>();
                foreach (var group in messages.GroupBy(message => (message.OriginatingAddress ?? "", message.TimestampMillis)))
                {
                    var sender = group.Key.Item1;
                    var body = string.Concat(group.Select(message => message.MessageBody ?? ""));
                    var timestamp = group.Key.Item2;
                    var receivedAt = DateTimeOffset.FromUnixTimeMilliseconds(timestamp).LocalDateTime;
                    await processor.ProcessAsync(CreateStableId(sender, timestamp, body), sender, body, receivedAt);
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
