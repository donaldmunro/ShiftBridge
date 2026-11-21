using ShiftBridge.Core.Config;
using ShiftBridge.Core.Gears;
using ShiftBridge.Core.Input;
using ShiftBridge.Core.Mapping;
using ShiftBridge.Core.Obc;
using ShiftBridge.Platform;

namespace ShiftBridge.Core.Tests;

public class InternalGearActionTests
{
    private static async Task Until(Func<bool> cond, int ms = 3000)
    {
        var end = Environment.TickCount64 + ms;
        while (!cond()) { if (Environment.TickCount64 > end) throw new TimeoutException(); await Task.Delay(10); }
    }

    [Fact]
    public void Center_gear_jumps_to_the_middle_and_does_nothing_when_already_there()
    {
        var gears = new VirtualGearState(24, 3);
        Assert.Equal(GearChange.Changed, gears.Apply(ObcButtonId.CenterGear));
        Assert.Equal(12, gears.Gear);
        Assert.Equal(GearChange.None, gears.Apply(ObcButtonId.CenterGear)); // déjà au centre : ni clignotement ni vibration
    }

    [Theory]
    [InlineData(20, 20)]
    [InlineData(99, 24)]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    public void Go_to_gear_is_bounded_to_the_scale(int target, int expected)
    {
        var gears = new VirtualGearState(24, 12);
        gears.Apply(ObcButtonId.GoToGear, target);
        Assert.Equal(expected, gears.Gear);
    }

    [Fact]
    public void Center_gear_follows_the_gear_count()
    {
        var gears = new VirtualGearState(12, 1);
        gears.Apply(ObcButtonId.CenterGear);
        Assert.Equal(6, gears.Gear);
    }

    [Fact]
    public void Internal_actions_are_known_and_listed_in_the_catalog()
    {
        Assert.True(ObcButtonCatalog.IsInternal(ObcButtonId.CenterGear));
        Assert.True(ObcButtonCatalog.IsInternal(ObcButtonId.GoToGear));
        Assert.False(ObcButtonCatalog.IsInternal(ObcButtonId.ShiftUp));
        var ids = ObcButtonCatalog.Assignable.Select(a => a.Id).ToList();
        Assert.Contains(ObcButtonId.CenterGear, ids);
        Assert.Contains(ObcButtonId.GoToGear, ids);
    }

    [Fact]
    public void Internal_actions_are_never_filtered_by_the_apps_declared_buttons()
    {
        var engine = new MappingEngine { SupportedActions = new HashSet<ObcButtonId> { ObcButtonId.Back } };
        engine.Profile.Bindings[PhysicalButton.RightA] = new ButtonBinding { Actions = [ObcButtonId.CenterGear] };
        engine.Profile.Bindings[PhysicalButton.RightY] = new ButtonBinding { Actions = [ObcButtonId.Emote] };

        Assert.Equal([(ObcButtonId.CenterGear, (byte)1)], engine.Translate([new ButtonEvent(PhysicalButton.RightA, true)]));
        Assert.Empty(engine.Translate([new ButtonEvent(PhysicalButton.RightY, true)])); // une action OBC reste filtrée
    }

    [Fact]
    public async Task Go_to_gear_changes_the_gear_and_is_not_sent_to_the_app()
    {
        await using var bridge = new BridgeService(new AppConfig { GearCount = 24, LastGear = 12 });
        var app = new FakeTransport("appli");
        await bridge.AddTransportAsync(app);
        bridge.Profile.Bindings[PhysicalButton.RightA] = new ButtonBinding { Actions = [ObcButtonId.GoToGear], GearTarget = 20 };
        bridge.Profile.Bindings[PhysicalButton.RightB] = new ButtonBinding { Actions = [ObcButtonId.CenterGear] };

        bridge.InjectEvents([new ButtonEvent(PhysicalButton.RightA, true)]);
        bridge.InjectEvents([new ButtonEvent(PhysicalButton.RightA, false)]);
        Assert.Equal(20, bridge.Gear.Gear);

        bridge.InjectEvents([new ButtonEvent(PhysicalButton.RightB, true)]);
        bridge.InjectEvents([new ButtonEvent(PhysicalButton.RightB, false)]);
        Assert.Equal(12, bridge.Gear.Gear);

        // Une vraie action, ensuite : c'est la seule qui arrive à l'appli (appuyée puis relâchée).
        bridge.InjectEvents([new ButtonEvent(PhysicalButton.ClickPlus, true)]);
        bridge.InjectEvents([new ButtonEvent(PhysicalButton.ClickPlus, false)]);
        await Until(() => app.Sent.Count == 2);
        await Task.Delay(50);
        Assert.Equal(
            [new byte[] { 0x01, (byte)ObcButtonId.ShiftUp, 1 }, new byte[] { 0x01, (byte)ObcButtonId.ShiftUp, 0 }],
            app.Sent.ToArray());
        Assert.Equal(13, bridge.Gear.Gear);
    }

    [Fact]
    public async Task Go_to_gear_works_even_when_the_app_declares_no_gear_buttons()
    {
        await using var bridge = new BridgeService(new AppConfig { GearCount = 24, LastGear = 5 });
        var app = new FakeTransport("appli");
        await bridge.AddTransportAsync(app);
        // Appli qui ne déclare que « Retour » : la touche interne doit passer quand même.
        typeof(BridgeService).GetMethod("OnAppInfo", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .Invoke(bridge, [new ObcAppInfo("test", "1", [(byte)ObcButtonId.Back])]);
        bridge.Profile.Bindings[PhysicalButton.RightA] = new ButtonBinding { Actions = [ObcButtonId.GoToGear], GearTarget = 18 };

        bridge.InjectEvents([new ButtonEvent(PhysicalButton.RightA, true)]);
        Assert.Equal(18, bridge.Gear.Gear);
    }

    [Fact]
    public void The_target_gear_is_saved_in_the_profile_and_survives_a_clone()
    {
        var profile = new Profile();
        profile.Bindings[PhysicalButton.RightA] = new ButtonBinding { Actions = [ObcButtonId.GoToGear], GearTarget = 17 };
        Assert.Equal(17, profile.Clone().Bindings[PhysicalButton.RightA].GearTarget);

        string path = Path.Combine(Path.GetTempPath(), $"shiftbridge-test-{Guid.NewGuid():N}.json");
        try
        {
            ConfigStore.Save(new AppConfig { Profile = profile }, path);
            var back = ConfigStore.Load(path).Profile.Bindings[PhysicalButton.RightA];
            Assert.Equal(17, back.GearTarget);
            Assert.Equal([ObcButtonId.GoToGear], back.Actions);
        }
        finally { File.Delete(path); }
    }
}
