using ShiftBridge.Core.Input;
using ShiftBridge.Platform.Input;

namespace ShiftBridge.Core.Tests;

public class InputAggregatorTests
{
    private static readonly ButtonKey A = ButtonKey.Pad("A");

    [Fact]
    public void Two_devices_holding_the_same_button_give_one_press_and_one_release()
    {
        var agg = new InputAggregator();
        var d1 = agg.Replace("x0", new HashSet<ButtonKey> { A });
        var d2 = agg.Replace("x1", new HashSet<ButtonKey> { A });
        var u1 = agg.Replace("x0", new HashSet<ButtonKey>());
        var u2 = agg.Replace("x1", new HashSet<ButtonKey>());

        Assert.Equal(new ButtonEvent(A, true), Assert.Single(d1));
        Assert.Empty(d2);
        Assert.Empty(u1);
        Assert.Equal(new ButtonEvent(A, false), Assert.Single(u2));
    }

    [Fact]
    public void Removing_a_source_releases_what_it_held()
    {
        var agg = new InputAggregator();
        agg.Replace("x0", new HashSet<ButtonKey> { A, ButtonKey.Pad("B") });
        var ev = agg.RemoveSource("x0");
        Assert.Equal(2, ev.Count);
        Assert.All(ev, e => Assert.False(e.Pressed));
    }

    [Fact]
    public void Removing_one_source_keeps_a_button_held_by_another()
    {
        var agg = new InputAggregator();
        agg.Replace("x0", new HashSet<ButtonKey> { A });
        agg.Replace("x1", new HashSet<ButtonKey> { A });
        Assert.Empty(agg.RemoveSource("x0"));
        Assert.Single(agg.RemoveSource("x1"));
    }

    [Fact]
    public void Keyboard_autorepeat_style_duplicate_downs_are_ignored()
    {
        var agg = new InputAggregator();
        var k = ButtonKey.Keyboard("F13");
        Assert.Single(agg.Set("kbd", k, true));
        Assert.Empty(agg.Set("kbd", k, true));
        Assert.Single(agg.Set("kbd", k, false));
        Assert.Empty(agg.Set("kbd", k, false));
    }

    [Fact]
    public void Clear_releases_everything_once()
    {
        var agg = new InputAggregator();
        agg.Set("kbd", ButtonKey.Keyboard("F13"), true);
        agg.Replace("x0", new HashSet<ButtonKey> { A });
        var ev = agg.Clear();
        Assert.Equal(2, ev.Count);
        Assert.All(ev, e => Assert.False(e.Pressed));
        Assert.Empty(agg.Clear());
    }
}

public class XInputMapperTests
{
    [Fact]
    public void Digital_buttons_map_to_pad_keys()
    {
        var m = new XInputMapper();
        var set = m.Map(new XInputSnapshot(0x1000 | 0x0100 | 0x0001, 0, 0, 0, 0, 0, 0));
        Assert.Contains(ButtonKey.Pad("A"), set);
        Assert.Contains(ButtonKey.Pad("LB"), set);
        Assert.Contains(ButtonKey.Pad("DPadUp"), set);
        Assert.Equal(3, set.Count);
    }

    [Fact]
    public void Trigger_uses_hysteresis()
    {
        var m = new XInputMapper();
        var lt = ButtonKey.Pad("LT");
        Assert.DoesNotContain(lt, m.Map(new XInputSnapshot(0, 90, 0, 0, 0, 0, 0)));   // sous le seuil d'appui
        Assert.Contains(lt, m.Map(new XInputSnapshot(0, 110, 0, 0, 0, 0, 0)));        // appuyée
        Assert.Contains(lt, m.Map(new XInputSnapshot(0, 80, 0, 0, 0, 0, 0)));         // entre les deux seuils : reste appuyée
        Assert.DoesNotContain(lt, m.Map(new XInputSnapshot(0, 50, 0, 0, 0, 0, 0)));   // relâchée
        Assert.DoesNotContain(lt, m.Map(new XInputSnapshot(0, 80, 0, 0, 0, 0, 0)));   // et ne se redéclenche pas à 80
    }

    [Fact]
    public void Stick_directions_and_hysteresis()
    {
        var m = new XInputMapper();
        var right = ButtonKey.Pad("LStickRight");
        Assert.Contains(right, m.Map(new XInputSnapshot(0, 0, 0, 20000, 0, 0, 0)));
        Assert.Contains(right, m.Map(new XInputSnapshot(0, 0, 0, 12000, 0, 0, 0)));
        Assert.DoesNotContain(right, m.Map(new XInputSnapshot(0, 0, 0, 5000, 0, 0, 0)));

        var set = m.Map(new XInputSnapshot(0, 0, 0, 0, -20000, 0, 20000));
        Assert.Contains(ButtonKey.Pad("LStickDown"), set);
        Assert.Contains(ButtonKey.Pad("RStickUp"), set);
    }

    [Fact]
    public void Centered_idle_controller_presses_nothing() =>
        Assert.Empty(new XInputMapper().Map(new XInputSnapshot(0, 0, 0, 800, -800, 0, 0)));
}

public class HidMappingTests
{
    [Theory]
    [InlineData(0, 0, 7, 0)]
    [InlineData(1, 1, 8, 0)]     // hat 1..8 : 1 = haut
    [InlineData(8, 1, 8, 7)]
    [InlineData(8, 0, 7, -1)]    // hat 0..7 : 8 = neutre
    [InlineData(15, 0, 7, -1)]
    [InlineData(2, 0, 3, 4)]     // hat à 4 positions : étalé sur 0,2,4,6
    public void Hat_index_is_normalised(int raw, int min, int max, int expected) =>
        Assert.Equal(expected, HidMapping.HatIndex(raw, min, max));

    [Fact]
    public void Hat_diagonals_give_two_directions()
    {
        Assert.Equal(["HatUp", "HatRight"], HidMapping.HatDirections(1));
        Assert.Equal(["HatDown"], HidMapping.HatDirections(4));
        Assert.Empty(HidMapping.HatDirections(-1));
    }

    [Fact]
    public void Axis_direction_has_hysteresis()
    {
        Assert.Equal(0, HidMapping.AxisDirection(0.5, 0));
        Assert.Equal(1, HidMapping.AxisDirection(0.8, 0));
        Assert.Equal(1, HidMapping.AxisDirection(0.65, 1));   // encore au-delà du seuil de relâchement
        Assert.Equal(0, HidMapping.AxisDirection(0.55, 1));
        Assert.Equal(-1, HidMapping.AxisDirection(0.1, 0));
        Assert.Equal(0, HidMapping.AxisDirection(0.65, 0));   // ne s'enclenche pas sous le seuil d'appui
    }
}

public class KeyNamesTests
{
    [Theory]
    [InlineData(0x41, false, "A")]
    [InlineData(0x35, false, "5")]
    [InlineData(0x7C, false, "F13")]
    [InlineData(0x70, false, "F1")]
    [InlineData(0x87, false, "F24")]
    [InlineData(0x65, false, "Num5")]
    [InlineData(0x0D, false, "Enter")]
    [InlineData(0x0D, true, "NumEnter")]
    [InlineData(0xB0, false, "MediaNextTrack")]
    [InlineData(0xE7, false, "VKE7")]
    public void Virtual_key_names(int vk, bool ext, string expected) =>
        Assert.Equal(expected, KeyboardKeyNames.NameFor(vk, ext));

    [Fact]
    public void French_labels()
    {
        Assert.Equal("Clavier : Piste suivante", ButtonKeyNames.Label(ButtonKey.Keyboard("MediaNextTrack")));
        Assert.Equal("Manette : Gâchette gauche (LT)", ButtonKeyNames.Label(ButtonKey.Pad("LT")));
        Assert.Equal("Joystick 046D_C21D : Bouton 3", ButtonKeyNames.Label(ButtonKey.Joystick("046D_C21D", "B3")));
        Assert.Equal("Joystick 046D_C21D : Axe X +", ButtonKeyNames.Label(ButtonKey.Joystick("046D_C21D", "XPlus")));
        Assert.Equal("ClickPlus", ButtonKeyNames.Label(PhysicalButton.ClickPlus));
    }
}
