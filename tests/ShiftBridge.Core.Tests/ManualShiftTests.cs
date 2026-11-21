using ShiftBridge.Core.Config;
using ShiftBridge.Core.Obc;
using ShiftBridge.Platform;

namespace ShiftBridge.Core.Tests;

/// <summary>Vitesse + / − demandées hors manette (boutons de la notification Android).</summary>
public class ManualShiftTests
{
    private static async Task Until(Func<bool> cond, int ms = 3000)
    {
        var end = Environment.TickCount64 + ms;
        while (!cond()) { if (Environment.TickCount64 > end) throw new TimeoutException(); await Task.Delay(10); }
    }

    [Fact]
    public async Task Shift_up_changes_the_gear_and_the_app_sees_a_short_press()
    {
        await using var bridge = new BridgeService(new AppConfig { GearCount = 24, LastGear = 10 });
        var app = new FakeTransport("appli");
        await bridge.AddTransportAsync(app);

        bridge.Shift(ObcButtonId.ShiftUp);

        Assert.Equal(11, bridge.Gear.Gear);
        await Until(() => app.Sent.Count >= 1);
        await Task.Delay(50);
        // Appui puis relâchement (dans un même message ou deux : l'ordre compte).
        var pairs = app.Sent.SelectMany(m => m.Skip(1).Chunk(2).Select(p => (p[0], p[1]))).ToList();
        Assert.Equal([((byte)ObcButtonId.ShiftUp, (byte)1), ((byte)ObcButtonId.ShiftUp, (byte)0)], pairs);
    }

    [Fact]
    public async Task Shift_down_stops_at_the_lowest_gear()
    {
        await using var bridge = new BridgeService(new AppConfig { GearCount = 24, LastGear = 1 });
        bridge.Shift(ObcButtonId.ShiftDown);
        Assert.Equal(1, bridge.Gear.Gear);
    }

    [Fact]
    public async Task Other_actions_are_ignored()
    {
        await using var bridge = new BridgeService(new AppConfig { GearCount = 24, LastGear = 10 });
        var app = new FakeTransport("appli");
        await bridge.AddTransportAsync(app);

        bridge.Shift(ObcButtonId.Menu);
        bridge.Shift(ObcButtonId.GoToGear);

        await Task.Delay(100);
        Assert.Equal(10, bridge.Gear.Gear);
        Assert.Empty(app.Sent);
    }

    [Fact]
    public async Task An_app_that_did_not_declare_the_gear_buttons_gets_nothing_but_the_gear_still_changes()
    {
        await using var bridge = new BridgeService(new AppConfig { GearCount = 24, LastGear = 10 });
        var app = new FakeTransport("appli");
        await bridge.AddTransportAsync(app);
        app.RaiseAppInfo(new ObcAppInfo("test", "1", [(byte)ObcButtonId.Back]));

        bridge.Shift(ObcButtonId.ShiftUp);

        await Task.Delay(100);
        Assert.Equal(11, bridge.Gear.Gear);
        Assert.Empty(app.Sent);
    }
}
