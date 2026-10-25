namespace PaiPai.Mobile.Platforms.Android;

public sealed class SmsReceivePermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [(global::Android.Manifest.Permission.ReceiveSms, true)];
}
