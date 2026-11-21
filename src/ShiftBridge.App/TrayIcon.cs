using Microsoft.Win32;
using ShiftBridge.Core.Localization;

namespace ShiftBridge.App;

/// <summary>Icône de la zone de notification : double-clic ou « Ouvrir » restaure la fenêtre, « Quitter » ferme l'application.</summary>
internal sealed class TrayIcon : IDisposable
{
    private readonly System.Windows.Forms.NotifyIcon _icon;
    private readonly System.Windows.Forms.ContextMenuStrip _menu = new();

    public event Action? OpenRequested;
    public event Action? QuitRequested;

    public TrayIcon(string tooltip)
    {
        _menu.Items.Add(Strings.Tray_Open, null, (_, _) => OpenRequested?.Invoke());
        _menu.Items.Add(Strings.Tray_Quit, null, (_, _) => QuitRequested?.Invoke());
        _icon = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            ContextMenuStrip = _menu,
            Visible = true,
        };
        SetTooltip(tooltip);
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke();
    }

    private static System.Drawing.Icon LoadIcon()
    {
        try
        {
            if (Environment.ProcessPath is { } exe && System.Drawing.Icon.ExtractAssociatedIcon(exe) is { } icon) return icon;
        }
        catch { /* repli sur l'icône générique */ }
        return System.Drawing.SystemIcons.Application;
    }

    /// <summary>L'info-bulle d'une NotifyIcon est limitée à 63 caractères.</summary>
    public void SetTooltip(string text) => _icon.Text = text.Length > 63 ? text[..63] : text;

    public void Dispose()
    {
        _icon.Visible = false;
        _icon.Dispose();
        _menu.Dispose();
    }
}

/// <summary>« Lancer avec Windows » : valeur de la clé Run de l'utilisateur (aucun droit administrateur requis).</summary>
internal static class StartupRegistration
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "ShiftBridge";

    public static void Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true) ?? Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled && Environment.ProcessPath is { } exe) key.SetValue(ValueName, $"\"{exe}\" --minimized");
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch { /* registre inaccessible : le réglage reste sans effet, rien de grave */ }
    }
}
