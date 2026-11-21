using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ShiftBridge.Presentation;

namespace ShiftBridge.App;

/// <summary>
/// Petite fenêtre toujours au premier plan qui affiche le rapport virtuel au-dessus de l'appli d'entraînement. Déplaçable
/// (glisser) ; une fois « verrouillée », les clics la traversent. Ne prend jamais le focus. Invisible au-dessus d'un plein
/// écran exclusif (une appli en fenêtre sans bordure ne pose pas de problème).
/// </summary>
public partial class GearOverlayWindow : Window
{
    private const int GwlExStyle = -20;
    private const int WsExTransparent = 0x20, WsExToolWindow = 0x80, WsExNoActivate = 0x08000000;

    [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int index, int value);

    private readonly MainViewModel _vm;

    public GearOverlayWindow(MainViewModel vm)
    {
        _vm = vm;
        InitializeComponent();
        DataContext = vm;
        _vm.PropertyChanged += OnViewModelChanged;
        _vm.GearFlashRequested += Flash;
        Loaded += (_, _) => { Place(); ApplyLock(); };
        Closed += (_, _) =>
        {
            _vm.PropertyChanged -= OnViewModelChanged;
            _vm.GearFlashRequested -= Flash;
            // Fermée par l'utilisateur : la case se décoche. Fermée avec l'application : la préférence est conservée.
            if (!ClosingWithApp && _vm.ShowOverlay) _vm.ShowOverlay = false;
        };
    }

    public bool ClosingWithApp { get; set; }

    private static readonly System.Windows.Media.Brush DefaultFrame = Freeze(new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)));

    private static System.Windows.Media.Brush Freeze(System.Windows.Media.Brush b) { b.Freeze(); return b; }

    /// <summary>Le cadre s'éclaire un instant à chaque changement : couleur d'accent, ou rouge quand la butée est atteinte.</summary>
    private void Flash(ShiftBridge.Core.Gears.GearChange change)
    {
        bool limit = change is ShiftBridge.Core.Gears.GearChange.AtTopLimit or ShiftBridge.Core.Gears.GearChange.AtBottomLimit;
        var source = Application.Current.TryFindResource(limit ? "Danger" : "Accent") as System.Windows.Media.SolidColorBrush;
        var brush = new System.Windows.Media.SolidColorBrush(source?.Color ?? System.Windows.Media.Colors.White);
        Frame.BorderBrush = brush;
        Frame.BorderThickness = new Thickness(3);
        var fade = new System.Windows.Media.Animation.DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(450));
        fade.Completed += (_, _) =>
        {
            // Un autre clignotement a pu démarrer entre-temps : on ne remet le cadre normal que si c'est encore le nôtre.
            if (!ReferenceEquals(Frame.BorderBrush, brush)) return;
            Frame.BorderBrush = DefaultFrame;
            Frame.BorderThickness = new Thickness(1);
        };
        brush.BeginAnimation(System.Windows.Media.Brush.OpacityProperty, fade);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Jamais dans la liste Alt+Tab, jamais activée : cliquer dessus ne retire pas le focus à l'appli d'entraînement.
        var hwnd = new WindowInteropHelper(this).Handle;
        SetWindowLong(hwnd, GwlExStyle, GetWindowLong(hwnd, GwlExStyle) | WsExToolWindow | WsExNoActivate);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.OverlayLocked)) ApplyLock();
    }

    /// <summary>Verrouillée : les clics traversent la fenêtre (WS_EX_TRANSPARENT).</summary>
    private void ApplyLock()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        int style = GetWindowLong(hwnd, GwlExStyle);
        SetWindowLong(hwnd, GwlExStyle, _vm.OverlayLocked ? style | WsExTransparent : style & ~WsExTransparent);
    }

    /// <summary>Position mémorisée si elle est encore sur un écran ; sinon coin supérieur droit de l'écran principal.</summary>
    private void Place()
    {
        if (_vm.SavedOverlayLeft is { } l && _vm.SavedOverlayTop is { } t
            && l > SystemParameters.VirtualScreenLeft - ActualWidth + 40 && l < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 40
            && t > SystemParameters.VirtualScreenTop - 10 && t < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 40)
        {
            Left = l; Top = t;
            return;
        }
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 24;
        Top = area.Top + 24;
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_vm.OverlayLocked) return;
        try { DragMove(); } catch (InvalidOperationException) { /* bouton déjà relâché */ }
        _vm.SaveOverlayPosition(Left, Top);
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        // Molette : taille de la superposition (0,5× à 3×).
        _vm.OverlayScale = Math.Clamp(Math.Round(_vm.OverlayScale + (e.Delta > 0 ? 0.1 : -0.1), 1), 0.5, 3.0);
    }
}
