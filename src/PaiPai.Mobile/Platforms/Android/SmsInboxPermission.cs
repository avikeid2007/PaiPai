using Android;

namespace PaiPai.Mobile.Platforms.Android;

public sealed class SmsInboxPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        [(Manifest.Permission.ReadSms, true)];
}
