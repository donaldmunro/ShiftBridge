using ShiftBridge.Core.Localization;
using Android.App;
using Android.Content.PM;
using AndroidPermission = Android.Manifest.Permission;

namespace ShiftBridge.Mobile;

/// <summary>Permissions d'exécution du pont : Bluetooth (Android 12+ : scan, connexion, annonce ; avant : localisation) et notifications.</summary>
internal static class BridgePermissions
{
    public static List<string> Missing(Activity activity)
    {
        var wanted = new List<string>();
        if (OperatingSystem.IsAndroidVersionAtLeast(31))
        {
            wanted.Add(AndroidPermission.BluetoothScan);
            wanted.Add(AndroidPermission.BluetoothConnect);
            wanted.Add(AndroidPermission.BluetoothAdvertise);
        }
        else wanted.Add(AndroidPermission.AccessFineLocation); // le scan BLE l'exige avant Android 12

        if (OperatingSystem.IsAndroidVersionAtLeast(33)) wanted.Add(AndroidPermission.PostNotifications);

        return [.. wanted.Where(p => activity.CheckSelfPermission(p) != Permission.Granted)];
    }

    /// <summary>
    /// Texte du bandeau d'avertissement si une permission Bluetooth manque, sinon null. Les notifications seules ne bloquent
    /// pas le pont (le service tourne, mais sa notification n'est pas visible) : pas de bandeau pour elles.
    /// </summary>
    public static string? BluetoothProblem(Activity activity)
    {
        bool bluetoothMissing = Missing(activity).Any(p => p != AndroidPermission.PostNotifications);
        if (!bluetoothMissing) return null;
        return OperatingSystem.IsAndroidVersionAtLeast(31)
            ? Strings.Perm_BluetoothDenied
            : Strings.Perm_LocationNeeded;
    }
}
