using ShiftBridge.Platform.Android;
using ShiftBridge.Presentation;

namespace ShiftBridge.Mobile;

/// <summary>
/// Le pont (ViewModel + BridgeService) vit au niveau du PROCESSUS, pas de l'activité : il continue de tourner écran éteint ou
/// quand l'appli d'entraînement est au premier plan, grâce au service de premier plan qui garde le processus en vie.
/// </summary>
internal static class AppRuntime
{
    private static readonly object Gate = new();
    private static MainViewModel? _viewModel;
    private static int _started;

    public static MainViewModel ViewModel
    {
        get
        {
            lock (Gate)
            {
                if (_viewModel is not null) return _viewModel;
                var vm = new MainViewModel(
                    new AndroidBlePlatform(global::Android.App.Application.Context), new MauiDispatcher(), new MauiAppHost());
                // Le journal de l'appli est aussi dans logcat (tag « ShiftBridge ») : utile pour diagnostiquer une coupure
                // quand l'écran est éteint ou que l'appareil est branché en adb.
                vm.LineLogged += line => global::Android.Util.Log.Info("ShiftBridge", line);
                return _viewModel = vm;
            }
        }
    }

    /// <summary>Démarre le pont une seule fois (l'activité et le redémarrage du service peuvent tous deux le demander).</summary>
    public static async Task StartAsync()
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;
        try { await ViewModel.StartAsync(); }
        catch { Interlocked.Exchange(ref _started, 0); throw; }
    }

    /// <summary>Arrête le pont et libère les connexions (sinon la manette reste « occupée »).</summary>
    public static async Task StopAsync()
    {
        if (Interlocked.Exchange(ref _started, 0) == 0) return;
        // Au-delà de 5 s on quitte quand même : une liaison qui ne répond plus ne doit pas laisser le processus en vie.
        try { await ViewModel.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch { /* délai dépassé ou erreur : on quitte quand même */ }
    }
}

internal sealed class MauiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        if (MainThread.IsMainThread) action();
        else MainThread.BeginInvokeOnMainThread(action);
    }
}

internal sealed class MauiAppHost : IAppHost
{
    public string AppVersion => AppInfo.Current.VersionString;

    public void CopyText(string text)
    {
        try { MainThread.BeginInvokeOnMainThread(async () => { try { await Clipboard.Default.SetTextAsync(text); } catch { } }); }
        catch { /* presse-papiers indisponible */ }
    }

    /// <summary>Réglages de l'application (là où l'utilisateur accorde « Appareils à proximité » après un refus définitif).</summary>
    public void OpenAppSettings()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var intent = new global::Android.Content.Intent(global::Android.Provider.Settings.ActionApplicationDetailsSettings,
                global::Android.Net.Uri.FromParts("package", context.PackageName, null));
            intent.AddFlags(global::Android.Content.ActivityFlags.NewTask);
            context.StartActivity(intent);
        }
        catch { /* aucun écran de réglages disponible */ }
    }

    /// <summary>Liste des exclusions d'optimisation de la batterie : sans elle, certains fabricants arrêtent le service de premier plan.</summary>
    public void OpenBatterySettings()
    {
        try
        {
            var intent = new global::Android.Content.Intent(global::Android.Provider.Settings.ActionIgnoreBatteryOptimizationSettings);
            intent.AddFlags(global::Android.Content.ActivityFlags.NewTask);
            global::Android.App.Application.Context.StartActivity(intent);
        }
        catch { /* écran absent sur cet appareil */ }
    }

    public void OpenUrl(string url)
    {
        try
        {
            var intent = new global::Android.Content.Intent(global::Android.Content.Intent.ActionView, global::Android.Net.Uri.Parse(url));
            intent.AddFlags(global::Android.Content.ActivityFlags.NewTask);
            global::Android.App.Application.Context.StartActivity(intent);
        }
        catch { /* aucun navigateur disponible */ }
    }
}
