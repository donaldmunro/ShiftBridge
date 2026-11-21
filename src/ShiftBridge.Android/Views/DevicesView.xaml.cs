using ShiftBridge.Core.Localization;
using ShiftBridge.Presentation;

namespace ShiftBridge.Mobile.Views;

public partial class DevicesView : ContentView
{
    public DevicesView() => InitializeComponent();

    private MainViewModel? Vm => BindingContext as MainViewModel;

    private static DeviceRowViewModel? RowOf(object? sender) => (sender as BindableObject)?.BindingContext as DeviceRowViewModel;

    private void OnToggleClicked(object? sender, EventArgs e)
    {
        if (RowOf(sender) is { } row) Vm?.ToggleDeviceCommand.Execute(row);
    }

    /// <summary>Menu « plus d'actions » : l'équivalent mobile du menu contextuel de la version Windows.</summary>
    private async void OnMoreClicked(object? sender, EventArgs e)
    {
        if (RowOf(sender) is not { } row || Vm is not { } vm || Window?.Page is not { } page) return;

        // Le détail technique (adresse, signal) n'a plus de ligne dédiée : il accompagne le titre du menu.
        string vibrate = Strings.Devices_Vibrate, forget = Strings.Devices_Forget, cancel = Strings.Buttons_Cancel;
        string heading = $"{row.Title}\n{row.Detail}";
        string? choice = row.CanVibrate
            ? await page.DisplayActionSheetAsync(heading, cancel, null, vibrate, forget)
            : await page.DisplayActionSheetAsync(heading, cancel, null, forget);

        if (choice == vibrate) vm.VibrateDeviceCommand.Execute(row);
        else if (choice == forget) vm.ForgetDeviceCommand.Execute(row);
    }
}
