using ShiftBridge.Core.DirCon;
using ShiftBridge.Core.Trainer;
using ShiftBridge.Platform.Ble;
using ShiftBridge.Platform.Sensors;
using ShiftBridge.Platform.Trainer;

namespace ShiftBridge.Core.Tests;

/// <summary>Capteur cardio en mémoire : service 0x180D (mesure en notification, position en lecture).</summary>
internal sealed class FakeHrLink : ITrainerLink
{
    public List<Guid> Subscribed = [];
    public byte[] Location = [0x02]; // poignet
    public bool Disposed;

    public bool IsConnected => !Disposed;
#pragma warning disable CS0067
    public event Action<Guid, byte[]>? Notification;
    public event Action? Disconnected;
    public event Action<string>? Log;
#pragma warning restore CS0067

    public Task<List<Guid>> DiscoverServicesAsync() => Task.FromResult(new List<Guid> { HeartRateCodec.Service });
    public Task<List<(Guid Uuid, byte Props)>> DiscoverCharacteristicsAsync(Guid service) =>
        Task.FromResult(new List<(Guid, byte)> { (HeartRateCodec.Measurement, DirConProps.Notify), (HeartRateCodec.SensorLocation, DirConProps.Read) });
    public Task<byte[]> ReadAsync(Guid ch) => Task.FromResult(Location);
    public Task WriteAsync(Guid ch, byte[] value) => Task.CompletedTask;
    public Task EnableNotificationsAsync(Guid ch, bool enable = true) { if (enable) Subscribed.Add(ch); return Task.CompletedTask; }
    public void Push(byte[] v) => Notification?.Invoke(HeartRateCodec.Measurement, v);
    public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
}

public class HeartRateTests
{
    private static async Task WaitUntil(Func<bool> cond, int ms = 3000)
    {
        var end = Environment.TickCount64 + ms;
        while (!cond()) { if (Environment.TickCount64 > end) throw new TimeoutException("condition non atteinte"); await Task.Delay(20); }
    }

    // ---------- Codec ----------

    [Fact]
    public void Parses_8bit_bpm()
    {
        Assert.True(HeartRateCodec.TryParse([0x00, 72], out var m));
        Assert.Equal(72, m.Bpm);
        Assert.Null(m.Contact);
    }

    [Fact]
    public void Parses_16bit_bpm()
    {
        Assert.True(HeartRateCodec.TryParse([0x01, 0x2C, 0x01], out var m)); // 300
        Assert.Equal(300, m.Bpm);
    }

    [Theory]
    [InlineData(0x04, false)] // bits 1-2 = 2 : contact non détecté
    [InlineData(0x06, true)]  // bits 1-2 = 3 : contact détecté
    public void Parses_sensor_contact(byte flags, bool expected)
    {
        Assert.True(HeartRateCodec.TryParse([flags, 60], out var m));
        Assert.Equal(expected, m.Contact);
    }

    [Fact]
    public void Skips_energy_and_counts_rr_intervals()
    {
        // flags : énergie (0x08) + RR (0x10), BPM 8 bits ; énergie 0x0102 ; deux intervalles RR
        Assert.True(HeartRateCodec.TryParse([0x18, 65, 0x02, 0x01, 0x00, 0x04, 0x10, 0x04], out var m));
        Assert.Equal(65, m.Bpm);
        Assert.Equal(0x0102, m.EnergyKj);
        Assert.Equal(2, m.RrCount);
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0x00 })]
    [InlineData(new byte[] { 0x01, 0x10 })]       // BPM 16 bits tronqué
    [InlineData(new byte[] { 0x08, 70, 0x01 })]   // énergie tronquée
    public void Rejects_truncated_frames(byte[] data) => Assert.False(HeartRateCodec.TryParse(data, out _));

    // ---------- Scanner ----------

    [Fact]
    public void Scanner_keeps_heart_rate_advertisements_and_ignores_the_rest()
    {
        var scanner = new FakeBleScanner();
        using var hub = new BleAdvertisementHub(scanner);
        using var sut = new BleHeartRateScanner(hub);
        var seen = new List<DiscoveredHeartRateSensor>();
        sut.SensorSeen += seen.Add;
        sut.Start();

        BleAdvertisement Adv(ulong addr, string? name, params Guid[] uuids) =>
            new(addr, name, -60, [], uuids, new Dictionary<Guid, byte[]>());

        scanner.Emit(Adv(1, "HRM-Dual", HeartRateCodec.Service));
        scanner.Emit(Adv(2, "KICKR", FtmsCodec.FitnessMachineService));
        scanner.Emit(Adv(3, "ShiftBridge cardio", HeartRateCodec.Service));
        scanner.Emit(Adv(4, null, HeartRateCodec.Service));

        Assert.Equal(["HRM-Dual", "Heart rate"], seen.Select(s => s.Name));
    }

    // ---------- Source ----------

    [Fact]
    public async Task Source_subscribes_reads_location_and_tracks_bpm()
    {
        var link = new FakeHrLink();
        await using var source = new HeartRateSource(_ => Task.FromResult<ITrainerLink>(link), "HRM");
        await source.ConnectAsync();

        Assert.Contains(HeartRateCodec.Measurement, link.Subscribed);
        Assert.Equal(new byte[] { 0x02 }, source.SensorLocation);
        Assert.Null(source.LastBpm);

        byte[]? relayed = null;
        source.Measurement += v => relayed = v;
        link.Push([0x00, 88]);
        Assert.Equal(88, source.LastBpm);
        Assert.Equal(new byte[] { 0x00, 88 }, relayed);
    }

    // ---------- Proxy ----------

    private static async Task<(FakeHrLink hr, HeartRateSource source, DirConTrainerProxy proxy, DirConClient app)> SetupAsync()
    {
        var hrLink = new FakeHrLink();
        var source = new HeartRateSource(_ => Task.FromResult<ITrainerLink>(hrLink), "HRM");
        await source.ConnectAsync();

        var proxy = new DirConTrainerProxy(new FakeLink(), new VirtualShiftingModel(), name: "HrTest") { HeartRate = source };
        await proxy.StartAsync();
        var app = new DirConClient();
        await app.ConnectAsync("127.0.0.1", proxy.Port);
        return (hrLink, source, proxy, app);
    }

    [Fact]
    public async Task Proxy_publishes_the_heart_rate_service_next_to_the_trainer()
    {
        var (_, source, proxy, app) = await SetupAsync();
        await using var _1 = source; await using var _2 = proxy; await using var _3 = app;

        var services = await app.DiscoverServicesAsync();
        Assert.Contains(FtmsCodec.FitnessMachineService, services);
        Assert.Contains(HeartRateCodec.Service, services);
        var chars = await app.DiscoverCharacteristicsAsync(HeartRateCodec.Service);
        Assert.Contains(chars, c => c.Uuid == HeartRateCodec.Measurement && (c.Props & DirConProps.Notify) != 0);
        Assert.Contains(chars, c => c.Uuid == HeartRateCodec.SensorLocation && (c.Props & DirConProps.Read) != 0);
    }

    [Fact]
    public async Task Proxy_relays_measurements_only_to_subscribed_apps_and_serves_the_location_locally()
    {
        var (hr, source, proxy, app) = await SetupAsync();
        await using var _1 = source; await using var _2 = proxy; await using var _3 = app;

        var received = new List<byte[]>();
        app.Notification += (ch, v) => { if (ch == HeartRateCodec.Measurement) received.Add(v); };

        hr.Push([0x00, 70]);                       // pas encore abonnée : rien ne doit arriver
        await Task.Delay(150);
        Assert.Empty(received);

        await app.EnableNotificationsAsync(HeartRateCodec.Measurement);
        hr.Push([0x00, 75]);
        await WaitUntil(() => received.Count == 1);
        Assert.Equal(new byte[] { 0x00, 75 }, received[0]);

        Assert.Equal(new byte[] { 0x02 }, await app.ReadAsync(HeartRateCodec.SensorLocation));
        Assert.Equal(75, proxy.LastHeartRateBpm);
    }

    [Fact]
    public async Task Proxy_refuses_writes_to_the_heart_rate_service()
    {
        var (_, source, proxy, app) = await SetupAsync();
        await using var _1 = source; await using var _2 = proxy; await using var _3 = app;

        await Assert.ThrowsAsync<DirConException>(() => app.WriteAsync(HeartRateCodec.Measurement, [0x01]));
    }

    // ---------- BridgeService ----------

    [Fact]
    public async Task Bridge_restarts_the_running_proxy_to_add_and_remove_the_sensor()
    {
        var connection = new FakeBleConnection(new BleServiceInfo(HeartRateCodec.Service,
        [
            new BleCharacteristicInfo(HeartRateCodec.Measurement, 0x10),
            new BleCharacteristicInfo(HeartRateCodec.SensorLocation, 0x02),
        ]));
        connection.ReadValues[(HeartRateCodec.Service, HeartRateCodec.SensorLocation)] = [0x01];
        var central = new FakeBleCentral { Connection = connection };
        var config = new ShiftBridge.Core.Config.AppConfig();

        await using var trainer = new FakeTrainer();
        await using var bridge = new ShiftBridge.Platform.BridgeService(config, new FakeBlePlatform(new FakeBleScanner(), central));
        await bridge.StartTrainerProxyAsync(new TrainerEndpoint.DirCon("127.0.0.1", trainer.Port));

        async Task<List<Guid>> ServicesSeenByAppAsync()
        {
            await using var app = new DirConClient();
            await app.ConnectAsync("127.0.0.1", bridge.TrainerProxyPort);
            return await app.DiscoverServicesAsync();
        }

        Assert.DoesNotContain(HeartRateCodec.Service, await ServicesSeenByAppAsync());

        await bridge.ConnectHeartRateAsync(42, "HRM-Test");
        Assert.True(bridge.HeartRateConnected);
        Assert.Equal("42", config.HeartRateBleAddress);
        Assert.Equal("HRM-Test", config.HeartRateBleName);
        Assert.True(bridge.TrainerProxyActive);
        Assert.Contains(HeartRateCodec.Service, await ServicesSeenByAppAsync());

        connection.Emit(HeartRateCodec.Service, HeartRateCodec.Measurement, [0x00, 91]);
        Assert.Equal(91, bridge.HeartRateBpm);

        await bridge.DisconnectHeartRateAsync();
        Assert.False(bridge.HeartRateConnected);
        Assert.Equal("", config.HeartRateBleAddress);
        Assert.True(bridge.TrainerProxyActive);
        Assert.DoesNotContain(HeartRateCodec.Service, await ServicesSeenByAppAsync());
    }

    [Fact]
    public async Task Proxy_without_a_sensor_does_not_expose_a_heart_rate_service()
    {
        var proxy = new DirConTrainerProxy(new FakeLink(), new VirtualShiftingModel(), name: "NoHr");
        await proxy.StartAsync();
        var app = new DirConClient();
        await app.ConnectAsync("127.0.0.1", proxy.Port);
        await using var _1 = proxy; await using var _2 = app;

        Assert.DoesNotContain(HeartRateCodec.Service, await app.DiscoverServicesAsync());
    }
}
