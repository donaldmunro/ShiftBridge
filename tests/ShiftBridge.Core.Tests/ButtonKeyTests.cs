using System.Text.Json;
using System.Text.Json.Serialization;
using ShiftBridge.Core.Config;
using ShiftBridge.Core.Input;
using ShiftBridge.Core.Mapping;
using ShiftBridge.Core.Obc;
using Xunit;

namespace ShiftBridge.Core.Tests;

public class ButtonKeyTests
{
    [Fact]
    public void Physical_button_converts_to_its_enum_name_and_back()
    {
        ButtonKey k = PhysicalButton.ClickPlus;
        Assert.Equal("ClickPlus", k.Id);
        Assert.True(k.IsZwift);
        Assert.True(k.TryAsPhysical(out var b));
        Assert.Equal(PhysicalButton.ClickPlus, b);
    }

    [Theory]
    [InlineData("kbd:F13", true, false, false)]
    [InlineData("pad:A", false, true, false)]
    [InlineData("joy:046D_C21D:B3", false, false, true)]
    public void Local_sources_are_recognised_and_not_zwift(string id, bool kbd, bool pad, bool joy)
    {
        var k = new ButtonKey(id);
        Assert.Equal(kbd, k.IsKeyboard);
        Assert.Equal(pad, k.IsPad);
        Assert.Equal(joy, k.IsJoystick);
        Assert.True(k.IsLocalInput);
        Assert.False(k.IsZwift);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("42")]
    [InlineData("Nope")]
    public void Numeric_or_unknown_ids_are_not_zwift(string id) =>
        Assert.False(new ButtonKey(id).IsZwift);

    [Fact]
    public void Profile_roundtrips_with_mixed_keys()
    {
        var p = Profile.ZwiftLike();
        p.Bindings[ButtonKey.Keyboard("F13")] = new ButtonBinding { Actions = [ObcButtonId.ShiftUp], RepeatOnHold = true };
        p.Bindings[ButtonKey.Pad("A")] = new ButtonBinding { Actions = [ObcButtonId.Select] };

        var opts = new JsonSerializerOptions { Converters = { new JsonStringEnumConverter() } };
        var back = JsonSerializer.Deserialize<Profile>(JsonSerializer.Serialize(p, opts), opts)!;

        Assert.Equal(p.Bindings.Count, back.Bindings.Count);
        Assert.Equal(ObcButtonId.ShiftUp, back.Bindings[ButtonKey.Keyboard("F13")].Actions[0]);
        Assert.True(back.Bindings[ButtonKey.Keyboard("F13")].RepeatOnHold);
        Assert.Equal(ObcButtonId.Select, back.Bindings[ButtonKey.Pad("A")].Actions[0]);
    }

    [Fact]
    public void Legacy_config_with_enum_names_still_loads()
    {
        const string json = """
            { "Profile": { "Name": "Ancien", "Bindings": {
                "ClickPlus": { "Actions": ["ShiftUp"], "RepeatOnHold": false, "RepeatDelayMs": 500, "RepeatIntervalMs": 250 }
            } } }
            """;
        var path = Path.Combine(Path.GetTempPath(), $"sb-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, json);
            var cfg = ConfigStore.Load(path);
            Assert.Equal(ObcButtonId.ShiftUp, cfg.Profile.Bindings[PhysicalButton.ClickPlus].Actions[0]);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Repeat_timings_get_faster_with_each_preset()
    {
        var all = new[] { RepeatSpeed.Slow, RepeatSpeed.Normal, RepeatSpeed.Fast, RepeatSpeed.VeryFast }
            .Select(RepeatSpeeds.Timings).ToArray();
        Assert.Equal((500, 250), all[0]);
        Assert.Equal((300, 120), all[2]);
        Assert.Equal((200, 80), all[3]);
        for (int i = 1; i < all.Length; i++)
        {
            Assert.True(all[i].DelayMs < all[i - 1].DelayMs);
            Assert.True(all[i].IntervalMs < all[i - 1].IntervalMs);
        }
    }

    [Fact]
    public void Invalid_repeat_speed_in_config_falls_back_to_default()
    {
        var cfg = new AppConfig { RepeatSpeed = (RepeatSpeed)99 };
        cfg.Normalize();
        Assert.Equal(RepeatSpeeds.Default, cfg.RepeatSpeed);
        Assert.Equal(RepeatSpeed.Fast, new AppConfig().RepeatSpeed);
    }

    [Fact]
    public void Mapping_shares_refcount_between_zwift_and_keyboard_keys()
    {
        var engine = new MappingEngine { Profile = new Profile() };
        engine.Profile.Bindings[PhysicalButton.ClickPlus] = new ButtonBinding { Actions = [ObcButtonId.ShiftUp] };
        engine.Profile.Bindings[ButtonKey.Keyboard("F13")] = new ButtonBinding { Actions = [ObcButtonId.ShiftUp] };

        var down1 = engine.Translate([new ButtonEvent(PhysicalButton.ClickPlus, true)]);
        var down2 = engine.Translate([new ButtonEvent(ButtonKey.Keyboard("F13"), true)]);
        var up1 = engine.Translate([new ButtonEvent(PhysicalButton.ClickPlus, false)]);
        var up2 = engine.Translate([new ButtonEvent(ButtonKey.Keyboard("F13"), false)]);

        Assert.Single(down1);
        Assert.Empty(down2);   // déjà tenue par l'autre touche
        Assert.Empty(up1);     // encore tenue par le clavier
        Assert.Equal((ObcButtonId.ShiftUp, (byte)0), Assert.Single(up2));
    }
}
