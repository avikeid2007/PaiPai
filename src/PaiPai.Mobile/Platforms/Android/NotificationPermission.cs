namespace PaiPai.Mobile.Platforms.Android;

public sealed class NotificationPermission : Permissions.BasePlatformPermission
{
    public override (string androidPermission, bool isRuntime)[] RequiredPermissions =>
        OperatingSystem.IsAndroidVersionAtLeast(33)
            ? [(global::Android.Manifest.Permission.PostNotifications, true)]
            : [];
}
