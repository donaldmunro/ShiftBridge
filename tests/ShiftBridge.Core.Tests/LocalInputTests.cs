using System.Net.Sockets;
using ShiftBridge.Core.Config;
using ShiftBridge.Core.Input;
using ShiftBridge.Core.Mapping;
using ShiftBridge.Core.Obc;
using ShiftBridge.Platform;
using ShiftBridge.Platform.Input;
using ShiftBridge.Platform.Obc;

namespace ShiftBridge.Core.Tests;

internal sealed class FakeLocalInputProvider : ILocalInputProvider
{
    public IReadOnlyList<LocalInputDevice> Devices { get; set; } = [];
    public event Action? DevicesChanged;
    public event Action<IReadOnlyList<ButtonEvent>>? ButtonEvents;
    public event Action<string>? Log { add { } remove { } }
    public event Action<ButtonKey>? Captured;
    public bool CaptureNext { get; set; }
    public bool CanVibrate { get; set; }
    public int Vibrations;
    public bool KeyboardOn, GamepadsOn;
    public IReadOnlySet<ButtonKey> Bound { get; private set; } = new HashSet<ButtonKey>();
    public bool Suppress { get; private set; }
    /// <summary>Touches réellement avalées = écoutées ET avalage actif.</summary>
    public IReadOnlySet<ButtonKey> Suppressed => Suppress ? Bound : new HashSet<ButtonKey>();

    public void Configure(bool keyboard, bool gamepads) { KeyboardOn = keyboard; GamepadsOn = gamepads; }
    public void SetBoundKeys(IReadOnlySet<ButtonKey> keys, bool suppress) { Bound = keys; Suppress = suppress; }
    public Task VibrateAsync() { Interlocked.Increment(ref Vibrations); return Task.CompletedTask; }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Press(ButtonKey k, bool down) => ButtonEvents?.Invoke([new ButtonEvent(k, down)]);
    public void Capture(ButtonKey k) { CaptureNext = false; Captured?.Invoke(k); }
    public void RaiseDevicesChanged() => DevicesChanged?.Invoke();
}

public class LocalInputTests
{
    private static async Task<byte[]> ReadAsync(NetworkStream s, int n, CancellationToken ct)
    {
        var buf = new byte[n];
        await s.ReadExactlyAsync(buf, ct);
        return buf;
    }

    private static AppConfig ConfigWith(params (ButtonKey Key, ObcButtonId Action)[] bindings)
    {
        var cfg = new AppConfig { KeyboardInputEnabled = true, GamepadInputEnabled = true };
        cfg.Profile = new Profile();
        foreach (var (k, a) in bindings) cfg.Profile.Bindings[k] = new ButtonBinding { Actions = [a] };
        return cfg;
    }

    [Fact]
    public async Task Keyboard_event_reaches_the_trainer_app_as_obc()
    {
        var fake = new FakeLocalInputProvider();
        await using var bridge = new BridgeService(ConfigWith((ButtonKey.Keyboard("F13"), ObcButtonId.ShiftUp)), localInput: fake);
        var tcp = new ObcTcpServer("LocalInputTest");
        await bridge.AddTransportAsync(tcp);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", tcp.Port);
        var s = client.GetStream();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await ReadAsync(s, 3, cts.Token);

        fake.Press(ButtonKey.Keyboard("F13"), true);
        fake.Press(ButtonKey.Keyboard("F13"), false);

        Assert.Equal(new byte[] { 0x01, 0x01, 0x01 }, await ReadAsync(s, 3, cts.Token));
        Assert.Equal(new byte[] { 0x01, 0x01, 0x00 }, await ReadAsync(s, 3, cts.Token));
    }

    [Fact]
    public async Task Config_is_pushed_to_the_provider_and_only_bound_keyboard_keys_are_suppressed()
    {
        var fake = new FakeLocalInputProvider();
        var cfg = ConfigWith(
            (ButtonKey.Keyboard("F13"), ObcButtonId.ShiftUp),
            (ButtonKey.Keyboard("F14"), ObcButtonId.None),   // sans action : pas avalée
            (ButtonKey.Pad("A"), ObcButtonId.Select));       // manette : jamais avalée
        await using var bridge = new BridgeService(cfg, localInput: fake);

        Assert.True(fake.KeyboardOn);
        Assert.True(fake.GamepadsOn);
        Assert.Equal([ButtonKey.Keyboard("F13")], fake.Suppressed.ToArray());
    }

    [Fact]
    public async Task Suppressed_keys_follow_profile_changes_and_the_suppress_setting()
    {
        var fake = new FakeLocalInputProvider();
        var cfg = ConfigWith((ButtonKey.Keyboard("F13"), ObcButtonId.ShiftUp));
        await using var bridge = new BridgeService(cfg, localInput: fake);

        bridge.Profile.Bindings[ButtonKey.Keyboard("F15")] = new ButtonBinding { Actions = [ObcButtonId.Back] };
        bridge.RefreshLocalInput();
        Assert.Contains(ButtonKey.Keyboard("F15"), fake.Suppressed);

        cfg.SuppressBoundKeys = false;
        bridge.RefreshLocalInput();
        Assert.Empty(fake.Suppressed);
        Assert.Contains(ButtonKey.Keyboard("F15"), fake.Bound); // toujours écoutée, juste plus avalée

        cfg.SuppressBoundKeys = true;
        cfg.KeyboardInputEnabled = false;
        bridge.RefreshLocalInput();
        Assert.False(fake.KeyboardOn);
        Assert.Empty(fake.Bound);
        Assert.Empty(fake.Suppressed);
    }

    [Fact]
    public async Task Held_keyboard_key_reports_pressed_state_for_the_live_led()
    {
        var fake = new FakeLocalInputProvider();
        await using var bridge = new BridgeService(ConfigWith((ButtonKey.Pad("A"), ObcButtonId.Select)), localInput: fake);
        var seen = new List<(ButtonKey, bool)>();
        bridge.PhysicalButtonChanged += (k, d) => seen.Add((k, d));

        fake.Press(ButtonKey.Pad("A"), true);
        fake.Press(ButtonKey.Pad("A"), false);

        Assert.Equal([(ButtonKey.Pad("A"), true), (ButtonKey.Pad("A"), false)], seen);
    }

    [Fact]
    public async Task Gear_change_from_a_local_key_vibrates_the_gamepad()
    {
        var fake = new FakeLocalInputProvider { CanVibrate = true };
        await using var bridge = new BridgeService(ConfigWith((ButtonKey.Pad("RB"), ObcButtonId.ShiftUp)), localInput: fake);

        fake.Press(ButtonKey.Pad("RB"), true);

        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (fake.Vibrations == 0 && DateTime.UtcNow < deadline) await Task.Delay(10);
        Assert.True(fake.Vibrations >= 1);
    }

    [Fact]
    public async Task Held_local_key_with_repeat_keeps_shifting_until_released()
    {
        var fake = new FakeLocalInputProvider();
        var cfg = ConfigWith((ButtonKey.Keyboard("F13"), ObcButtonId.ShiftUp));
        cfg.Profile.Bindings[ButtonKey.Keyboard("F13")] = new ButtonBinding
            { Actions = [ObcButtonId.ShiftUp], RepeatOnHold = true };
        cfg.RepeatSpeed = RepeatSpeed.VeryFast;
        cfg.GearCount = 24;
        await using var bridge = new BridgeService(cfg, localInput: fake);
        int start = bridge.Gear.Gear;

        fake.Press(ButtonKey.Keyboard("F13"), true);
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (bridge.Gear.Gear < start + 3 && DateTime.UtcNow < deadline) await Task.Delay(10);
        fake.Press(ButtonKey.Keyboard("F13"), false);

        Assert.True(bridge.Gear.Gear >= start + 3);
        await Task.Delay(150);
        int after = bridge.Gear.Gear;
        await Task.Delay(150);
        Assert.Equal(after, bridge.Gear.Gear); // plus de répétition après le relâchement
    }
}
