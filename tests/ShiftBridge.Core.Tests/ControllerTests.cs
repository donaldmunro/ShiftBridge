using ShiftBridge.Core.Config;
using ShiftBridge.Core.Controllers;
using ShiftBridge.Core.Controllers.Decoders;
using ShiftBridge.Core.Input;
using ShiftBridge.Core.Mapping;
using ShiftBridge.Core.Obc;
using ShiftBridge.Platform;
using ShiftBridge.Platform.Ble;
using ShiftBridge.Platform.Controllers;

namespace ShiftBridge.Core.Tests;

public class ControllerDecoderTests
{
    private static byte[] Hex(string h) => Convert.FromHexString(h);

    // ---------- Thinkrider VS200 ----------

    [Fact]
    public void Vs200_up_and_down_frames_are_taps()
    {
        var d = new ThinkriderVs200Decoder();
        Assert.Equal(["Up"], d.Decode(ThinkriderVs200Decoder.Notify, Hex("F3050301FC"))!.Taps);
        Assert.Equal(["Down"], d.Decode(ThinkriderVs200Decoder.Notify, Hex("F3050300FB"))!.Taps);
        Assert.Null(d.Decode(ThinkriderVs200Decoder.Notify, Hex("F3050301FC"))!.Held);
    }

    [Theory]
    [InlineData("F3050301FD")] // somme de contrôle fausse
    [InlineData("F3050302FE")] // code inconnu
    [InlineData("F3050301")]   // trop court
    [InlineData("00050301FC")] // en-tête inconnu
    public void Vs200_rejects_unknown_frames(string hex) =>
        Assert.Null(new ThinkriderVs200Decoder().Decode(ThinkriderVs200Decoder.Notify, Hex(hex)));

    // ---------- CYCPLUS BC2 ----------

    [Fact]
    public void Bc2_tracks_lever_state_and_ignores_unexplained_value()
    {
        var d = new CycplusBc2Decoder();
        Guid c = BleUuids.NordicUartTx;
        // Capture publique : levier haut enfoncé (01), levier bas relâché (03).
        var f1 = d.Decode(c, Hex("FEEFFFEE02060103975E5E000155"))!;
        Assert.Equal(["Up"], f1.Held!.OrderBy(x => x));

        // Relâchement du haut.
        var f2 = d.Decode(c, Hex("FEEFFFEE02060303975E5E000155"))!;
        Assert.Empty(f2.Held!);

        // Appui long (04) puis valeur inexpliquée (02) : l'état est conservé.
        d.Decode(c, Hex("FEEFFFEE02060403975E5E000155"));
        var f3 = d.Decode(c, Hex("FEEFFFEE02060203975E5E000155"))!;
        Assert.Equal(["Up"], f3.Held!);

        // Les deux leviers.
        var f4 = d.Decode(c, Hex("FEEFFFEE02060101975E5E000155"))!;
        Assert.Equal(["Down", "Up"], f4.Held!.OrderBy(x => x));
    }

    [Fact]
    public void Bc2_rejects_wrong_header_and_short_frames()
    {
        var d = new CycplusBc2Decoder();
        Assert.Null(d.Decode(BleUuids.NordicUartTx, Hex("AABBCCDD02060103")));
        Assert.Null(d.Decode(BleUuids.NordicUartTx, Hex("FEEFFFEE0206")));
    }

    // ---------- Magene Onelap Key ----------

    [Fact]
    public void Onelap_long_press_is_held_until_next_frame()
    {
        var d = new OnelapKeyDecoder();
        // Appui long haut (b[10] = 04), puis la trame de relâchement publiée (tous les masques à 0).
        var press = d.Decode(OnelapKeyDecoder.Notify, Hex("8bf510e503000000000004"))!;
        Assert.Equal(["Up"], press.Held!);
        Assert.Empty(press.Taps);

        var release = d.Decode(OnelapKeyDecoder.Notify, Hex("8bf510e503000004000000"))!;
        Assert.Empty(release.Held!);
        Assert.Empty(release.Taps); // une trame à zéro n'est pas un clic
    }

    [Fact]
    public void Onelap_click_mask_gives_taps_and_unit_type_gives_prefix()
    {
        var d = new OnelapKeyDecoder();
        // b[5] = 0x01 (unité gauche), b[8] = 0x04|0x01 (haut + centre).
        var f = d.Decode(OnelapKeyDecoder.Notify, Hex("8bf510e503010000050000"))!;
        Assert.Equal(["L.Center", "L.Up"], f.Taps.OrderBy(x => x));
    }

    [Fact]
    public void Onelap_units_do_not_release_each_others_long_presses()
    {
        var d = new OnelapKeyDecoder();
        d.Decode(OnelapKeyDecoder.Notify, Hex("8bf510e503010000000004")); // gauche : haut tenu
        var f = d.Decode(OnelapKeyDecoder.Notify, Hex("8bf510e503020000000002"))!; // droite : gauche tenu
        Assert.Equal(["L.Up", "R.Left"], f.Held!.OrderBy(x => x));
        var g = d.Decode(OnelapKeyDecoder.Notify, Hex("8bf510e503020000000000"))!; // droite relâchée
        Assert.Equal(["L.Up"], g.Held!);
    }

    [Theory]
    [InlineData("8bf610e503000000000004")] // b[1] inattendu
    [InlineData("8bf510e5030000")]         // trop court
    [InlineData("8bf510e503030000000004")] // type d'unité > 2
    public void Onelap_rejects_unknown_frames(string hex) =>
        Assert.Null(new OnelapKeyDecoder().Decode(OnelapKeyDecoder.Notify, Hex(hex)));
}

public class ControllerCatalogTests
{
    private static BleAdvertisement Adv(string? name, Guid[]? services = null) =>
        FakeBleScanner.Adv(0x1, name, services: services);

    [Theory]
    [InlineData("Think VS01-0001725", ControllerKind.ThinkriderVs200)]
    [InlineData("THINK VS200", ControllerKind.ThinkriderVs200)]
    [InlineData("CYCPLUS BC2 57344", ControllerKind.CycplusBc2)]
    [InlineData("Onelap Key_12224", ControllerKind.OnelapKey)]
    [InlineData("  onelap key ", ControllerKind.OnelapKey)]
    public void Identifies_known_remotes_by_name(string name, ControllerKind expected) =>
        Assert.Equal(expected, ControllerCatalog.Identify(Adv(name)));

    [Theory]
    [InlineData("THINK X1")]        // home-trainer Thinkrider
    [InlineData("THINK-X7")]
    [InlineData("Think")]
    [InlineData("CYCPLUS T7H-L")]   // pas un BC2
    [InlineData("Onelap D100")]     // trainer Onelap
    [InlineData("")]
    [InlineData(null)]
    public void Ignores_similar_names(string? name) => Assert.Null(ControllerCatalog.Identify(Adv(name)));

    [Fact]
    public void Nordic_uart_or_shared_service_alone_never_matches()
    {
        Assert.Null(ControllerCatalog.Identify(Adv(null, [BleUuids.NordicUartService])));
        Assert.Null(ControllerCatalog.Identify(Adv("Kettler C12", [OnelapKeyDecoder.Service])));
    }

    [Fact]
    public void Scanner_reports_zwift_and_other_controllers()
    {
        var scanner = new FakeBleScanner();
        using var hub = new BleAdvertisementHub(scanner);
        using var cs = new ControllerScanner(hub);
        var seen = new List<DiscoveredController>();
        cs.DeviceSeen += seen.Add;
        cs.Start();

        scanner.Emit(FakeBleScanner.Adv(0x10, "Think VS01-1"));
        scanner.Emit(FakeBleScanner.Adv(0x11, "Casque"));
        scanner.Emit(FakeBleScanner.Adv(0x12, "Zwift Click", manufacturer: [(0x094A, [0x09])]));

        Assert.Equal([ControllerKind.ThinkriderVs200, ControllerKind.Zwift], seen.Select(d => d.Kind));
        Assert.IsType<DiscoveredZwiftDevice>(seen[1]);
    }

    [Fact]
    public void Trainer_scanner_does_not_list_remotes_as_trainers()
    {
        var scanner = new FakeBleScanner();
        using var hub = new BleAdvertisementHub(scanner);
        var ts = new ShiftBridge.Platform.Trainer.BleTrainerScanner(hub);
        var list = new List<ShiftBridge.Platform.Trainer.DiscoveredBleTrainer>();
        ts.TrainerSeen += list.Add;
        ts.Start();
        var ftms = new[] { ShiftBridge.Core.Trainer.FtmsCodec.BaseUuid(0x1826) };

        scanner.Emit(FakeBleScanner.Adv(0x20, "THINK VS01-1", services: ftms));
        scanner.Emit(FakeBleScanner.Adv(0x21, "THINK X1", services: ftms));

        Assert.Equal(["THINK X1"], list.Select(t => t.Name));
    }
}

public class BleButtonControllerTests
{
    private static readonly DiscoveredController Vs200Info = new(0xAABB00000001, "Think VS01-1", ControllerKind.ThinkriderVs200, -50);
    private static readonly DiscoveredController Bc2Info = new(0xAABB00000002, "CYCPLUS BC2 1", ControllerKind.CycplusBc2, -50);

    private static BleServiceInfo Vs200Service() =>
        new(ThinkriderVs200Decoder.Service, [new BleCharacteristicInfo(ThinkriderVs200Decoder.Notify, 0x10)]);

    private static BleServiceInfo Bc2Service() =>
        new(BleUuids.NordicUartService, [new BleCharacteristicInfo(BleUuids.NordicUartTx, 0x10)]);

    private static async Task WaitFor(Func<bool> condition, int timeoutMs = 3000)
    {
        var until = Environment.TickCount64 + timeoutMs;
        while (!condition())
        {
            if (Environment.TickCount64 > until) throw new TimeoutException("condition non atteinte");
            await Task.Delay(10);
        }
    }

    private static BleButtonController Make(FakeBleConnection conn, DiscoveredController info, bool autoReconnect = false) =>
        new(new FakeBleCentral { Connection = conn }, info, ControllerSpec.For(info.Kind)!, autoReconnect) { TapHoldMs = 1 };

    [Fact]
    public async Task Connects_and_subscribes_to_the_notify_characteristic()
    {
        var conn = new FakeBleConnection(Vs200Service());
        await using var c = Make(conn, Vs200Info);
        c.Start();
        await WaitFor(() => c.State == ControllerState.Connected);
        Assert.Equal(BleNotifyMode.Notify, conn.Notify[(ThinkriderVs200Decoder.Service, ThinkriderVs200Decoder.Notify)]);
        Assert.Empty(conn.Writes); // aucun handshake
        Assert.False(c.CanVibrate);
    }

    [Fact]
    public async Task Tap_becomes_press_then_release_in_order()
    {
        var conn = new FakeBleConnection(Vs200Service());
        await using var c = Make(conn, Vs200Info);
        var events = new List<ButtonEvent>();
        c.ButtonEvents += evs => { lock (events) events.AddRange(evs); };
        c.Start();
        await WaitFor(() => c.State == ControllerState.Connected);

        conn.Emit(ThinkriderVs200Decoder.Service, ThinkriderVs200Decoder.Notify, Convert.FromHexString("F3050301FC"));
        conn.Emit(ThinkriderVs200Decoder.Service, ThinkriderVs200Decoder.Notify, Convert.FromHexString("F3050300FB"));
        await WaitFor(() => { lock (events) return events.Count == 4; });

        var up = ButtonKey.Controller(ControllerKind.ThinkriderVs200, "Up");
        var down = ButtonKey.Controller(ControllerKind.ThinkriderVs200, "Down");
        Assert.Equal([new ButtonEvent(up, true), new ButtonEvent(up, false), new ButtonEvent(down, true), new ButtonEvent(down, false)], events);
    }

    [Fact]
    public async Task Held_levers_are_released_when_the_link_drops()
    {
        var conn = new FakeBleConnection(Bc2Service());
        await using var c = Make(conn, Bc2Info);
        var events = new List<ButtonEvent>();
        c.ButtonEvents += evs => { lock (events) events.AddRange(evs); };
        c.Start();
        await WaitFor(() => c.State == ControllerState.Connected);

        conn.Emit(BleUuids.NordicUartService, BleUuids.NordicUartTx, Convert.FromHexString("FEEFFFEE02060103975E5E000155"));
        var up = ButtonKey.Controller(ControllerKind.CycplusBc2, "Up");
        await WaitFor(() => { lock (events) return events.Count == 1; });
        Assert.Equal(new ButtonEvent(up, true), events[0]);

        conn.Drop();
        await WaitFor(() => { lock (events) return events.Count == 2; });
        Assert.Equal(new ButtonEvent(up, false), events[1]);
    }

    [Fact]
    public async Task Wrong_device_without_expected_service_ends_in_error()
    {
        var conn = new FakeBleConnection(); // ni FEA0 ni FEA1
        await using var c = Make(conn, Vs200Info);
        var states = new List<ControllerState>();
        var logs = new List<string>();
        c.StateChanged += s => { lock (states) states.Add(s); };
        c.Log += logs.Add;
        c.Start();
        // Sans reconnexion automatique l'état Error est aussitôt suivi de Disconnected : on vérifie qu'il a bien été signalé.
        await WaitFor(() => { lock (states) return states.Contains(ControllerState.Disconnected); });
        lock (states) Assert.Contains(ControllerState.Error, states);
        Assert.DoesNotContain(ControllerState.Connected, states);
    }

    [Fact]
    public async Task Unrecognised_frame_does_not_press_anything()
    {
        var conn = new FakeBleConnection(Vs200Service());
        await using var c = Make(conn, Vs200Info);
        var events = new List<ButtonEvent>();
        var frames = new List<string>();
        c.ButtonEvents += evs => events.AddRange(evs);
        c.Frame += frames.Add;
        c.Start();
        await WaitFor(() => c.State == ControllerState.Connected);

        conn.Emit(ThinkriderVs200Decoder.Service, ThinkriderVs200Decoder.Notify, [1, 2, 3]);
        Assert.Empty(events);
        Assert.Contains(frames, f => f.Contains("non reconnue"));
    }

    [Fact]
    public async Task Bridge_connects_a_remote_and_seeds_default_bindings_once()
    {
        var scanner = new FakeBleScanner();
        var central = new FakeBleCentral { Connection = new FakeBleConnection(Vs200Service()) };
        var config = new AppConfig();
        await using var bridge = new BridgeService(config, new FakeBlePlatform(scanner, central));
        bridge.StartScan();
        scanner.Emit(FakeBleScanner.Adv(0xAABB00000001, "Think VS01-1"));

        var device = Assert.Single(bridge.SeenDevices);
        Assert.Equal(ControllerKind.ThinkriderVs200, device.Kind);
        bridge.Connect(device);
        await WaitFor(() => bridge.Controllers.Any(c => c.State == ControllerState.Connected));

        var up = ButtonKey.Controller(ControllerKind.ThinkriderVs200, "Up");
        Assert.Equal([ObcButtonId.ShiftUp], config.Profile.Bindings[up].Actions);
        Assert.Equal(ControllerKind.ThinkriderVs200, config.KnownControllerKinds["AA:BB:00:00:00:01"]);

        // L'utilisateur retire l'assignation : une reconnexion ne la remet pas.
        config.Profile.Bindings.Remove(up);
        await bridge.DisconnectAsync(device.Address);
        bridge.Connect(device);
        await WaitFor(() => bridge.Controllers.Any(c => c.State == ControllerState.Connected));
        Assert.False(config.Profile.Bindings.ContainsKey(up));
    }
}

public class ControllerButtonKeyTests
{
    [Fact]
    public void Controller_keys_round_trip_and_stay_out_of_local_inputs()
    {
        var key = ButtonKey.Controller(ControllerKind.OnelapKey, "L.Up");
        Assert.Equal("ctl:onelap:L.Up", key.Id);
        Assert.True(key.IsController);
        Assert.False(key.IsLocalInput);
        Assert.False(key.IsZwift);
        Assert.True(key.TryAsController(out var kind, out var name));
        Assert.Equal(ControllerKind.OnelapKey, kind);
        Assert.Equal("L.Up", name);
        Assert.False(new ButtonKey("ctl:inconnu:Up").TryAsController(out _, out _));
    }

    [Fact]
    public void Every_button_has_a_unique_name_and_defaults_point_to_real_buttons()
    {
        foreach (var kind in Enum.GetValues<ControllerKind>().Where(k => k != ControllerKind.Zwift))
        {
            var names = kind.Buttons().Select(b => b.Name).ToList();
            Assert.NotEmpty(names);
            Assert.Equal(names.Count, names.Distinct().Count());
            foreach (var (name, _) in Profile.ControllerDefaults(kind)) Assert.Contains(name, names);
            Assert.NotNull(ControllerSpec.For(kind));
        }
    }

    [Fact]
    public void Apply_defaults_keeps_existing_bindings()
    {
        var p = new Profile();
        var up = ButtonKey.Controller(ControllerKind.CycplusBc2, "Up");
        p.Bindings[up] = new ButtonBinding { Actions = [ObcButtonId.Menu] };
        Assert.True(p.ApplyControllerDefaults(ControllerKind.CycplusBc2));
        Assert.Equal([ObcButtonId.Menu], p.Bindings[up].Actions);
        Assert.Equal([ObcButtonId.ShiftDown], p.Bindings[ButtonKey.Controller(ControllerKind.CycplusBc2, "Down")].Actions);
        Assert.False(p.ApplyControllerDefaults(ControllerKind.CycplusBc2));
    }

    [Fact]
    public void Config_round_trips_known_controller_kinds()
    {
        var cfg = new AppConfig();
        cfg.KnownControllerKinds["AA:BB:CC:DD:EE:FF"] = ControllerKind.OnelapKey;
        var json = System.Text.Json.JsonSerializer.Serialize(cfg, new System.Text.Json.JsonSerializerOptions
        {
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        });
        Assert.Contains("OnelapKey", json);
    }
}
