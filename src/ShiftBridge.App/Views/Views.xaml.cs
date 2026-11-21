using System.Windows;
using System.Windows.Controls;

namespace ShiftBridge.App.Views;

public partial class HomeView : UserControl
{
    public HomeView() => InitializeComponent();
}

public partial class ButtonsView : UserControl
{
    public ButtonsView() => InitializeComponent();
}

public partial class TrainerView : UserControl
{
    public TrainerView() => InitializeComponent();
}

public partial class AppsView : UserControl
{
    public AppsView() => InitializeComponent();
}

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();
}

public partial class DiagnosticView : UserControl
{
    public DiagnosticView() => InitializeComponent();
}

public partial class DevicesView : UserControl
{
    public DevicesView() => InitializeComponent();

    /// <summary>Ouvre le menu « ⋯ » (Vibrer, Oublier) sous le bouton.</summary>
    private void MoreButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }
}
