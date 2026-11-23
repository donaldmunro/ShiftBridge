using ShiftBridge.Presentation;

namespace ShiftBridge.Mobile.Views;

public partial class DiagnosticView : ContentView
{
    public DiagnosticView() => InitializeComponent();

    private void OnCaptureClicked(object? sender, EventArgs e)
    {
        if ((sender as BindableObject)?.BindingContext is InspectedDeviceRowViewModel row && BindingContext is MainViewModel vm)
            vm.CaptureInspectedCommand.Execute(row);
    }
}
