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
            // Sous le bouton, bord droit aligné sur le sien
            menu.Placement = System.Windows.Controls.Primitives.PlacementMode.Custom;
            menu.CustomPopupPlacementCallback = (popupSize, targetSize, _) =>
            [
                new System.Windows.Controls.Primitives.CustomPopupPlacement(
                    new Point(targetSize.Width - popupSize.Width, targetSize.Height),
                    System.Windows.Controls.Primitives.PopupPrimaryAxis.Horizontal)
            ];
            menu.IsOpen = true;
        }
    }
}
