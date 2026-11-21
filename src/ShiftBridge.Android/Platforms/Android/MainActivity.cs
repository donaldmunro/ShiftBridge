using ShiftBridge.Core.Localization;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;

namespace ShiftBridge.Mobile;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    private const int PermissionRequestCode = 4001;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        var missing = BridgePermissions.Missing(this);
        UpdatePermissionBanner();
        if (missing.Count == 0) StartBridge();
        else RequestPermissions([.. missing], PermissionRequestCode);
    }

    public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Permission[] grantResults)
    {
        base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        // On démarre quoi qu'il arrive : si le Bluetooth est refusé, le journal l'explique (« permission Bluetooth refusée »)
        // et l'utilisateur peut encore utiliser le réseau et le diagnostic.
        if (requestCode == PermissionRequestCode)
        {
            UpdatePermissionBanner();
            StartBridge();
        }
    }

    private bool _bluetoothWasMissing;

    /// <summary>
    /// Au retour des réglages système (l'utilisateur a pu accorder la permission) : on met le bandeau à jour et, si le Bluetooth
    /// est maintenant autorisé, on relance la recherche des manettes qui n'avait rien pu faire.
    /// </summary>
    protected override void OnResume()
    {
        base.OnResume();
        bool wasMissing = _bluetoothWasMissing;
        UpdatePermissionBanner();
        if (wasMissing && !_bluetoothWasMissing) AppRuntime.ViewModel.StartScanCommand.Execute(null);
    }

    private void UpdatePermissionBanner()
    {
        string? problem = BridgePermissions.BluetoothProblem(this);
        _bluetoothWasMissing = problem is not null;
        AppRuntime.ViewModel.SetPermissionProblem(problem);
    }

    private void StartBridge()
    {
        try { StartForegroundService(new Intent(this, typeof(BridgeForegroundService))); }
        catch (Exception ex) { AppRuntime.ViewModel.ReportError(Loc.Fmt(Strings.Err_ForegroundService, ex.Message)); }
        _ = StartRuntimeAsync();
    }

    private static async Task StartRuntimeAsync()
    {
        try { await AppRuntime.StartAsync(); }
        catch (Exception ex) { AppRuntime.ViewModel.ReportError(ex.Message); }
    }
}
