using System.Windows;
using ShiftBridge.Presentation;

namespace ShiftBridge.App;

/// <summary>Marshalisation vers le thread UI WPF.</summary>
internal sealed class WpfDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        var d = Application.Current?.Dispatcher;
        if (d is null) return;
        if (d.CheckAccess()) action(); else d.BeginInvoke(action);
    }
}

/// <summary>Services de l'application WPF pour le ViewModel partagé.</summary>
internal sealed class WpfAppHost : IAppHost
{
    public string AppVersion { get; } = typeof(WpfAppHost).Assembly.GetName().Version?.ToString(3) ?? "?";

    public IAppUpdater? Updater { get; } = new Updates.GitHubUpdater(typeof(WpfAppHost).Assembly.GetName().Version?.ToString(3) ?? "0.0.0");

    public bool Confirm(string message) =>
        MessageBox.Show(message, "ShiftBridge", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public void CopyText(string text)
    {
        try { Clipboard.SetText(text); } catch { /* presse-papiers occupé */ }
    }

    public void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* aucun navigateur disponible */ }
    }
}
