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

    /// <summary>Petite fenêtre de saisie d'une ligne de texte ; null si annulée.</summary>
    public Task<string?> PromptTextAsync(string message, string initial)
    {
        var box = new System.Windows.Controls.TextBox { Text = initial, MaxLength = 40, Margin = new Thickness(0, 8, 0, 14) };
        var ok = new System.Windows.Controls.Button { Content = ShiftBridge.Core.Localization.Strings.State_Ok, IsDefault = true, MinWidth = 80, Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new System.Windows.Controls.Button { Content = ShiftBridge.Core.Localization.Strings.Buttons_Cancel, IsCancel = true, MinWidth = 80 };
        var buttons = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);
        var panel = new System.Windows.Controls.StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new System.Windows.Controls.TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        panel.Children.Add(box);
        panel.Children.Add(buttons);
        var owner = Application.Current?.MainWindow;
        var window = new Window
        {
            Title = "ShiftBridge", Content = panel, Width = 380, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
            Owner = owner is { IsVisible: true } ? owner : null,
            WindowStartupLocation = owner is { IsVisible: true } ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
            ShowInTaskbar = false,
        };
        ok.Click += (_, _) => window.DialogResult = true;
        window.Loaded += (_, _) => { box.Focus(); box.SelectAll(); };
        return Task.FromResult(window.ShowDialog() == true ? box.Text : null);
    }

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
