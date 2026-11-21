using ShiftBridge.Core.DirCon;
using ShiftBridge.Core.Trainer;
using ShiftBridge.Core.Zwift;
using ShiftBridge.Platform.Trainer;

namespace ShiftBridge.Core.Tests;

public class NativeModeProxyTests
{
    private static readonly byte[] SimFlat = [0x11, 0x00, 0x00, 0x00, 0x00, 40, 51];
    private static readonly byte[] SimClimb = [0x11, 0x00, 0x00, 0x2C, 0x01, 40, 51];   // +3 %

    private static async Task<(DirConClient up, DirConTrainerProxy proxy, DirConClient app)> SetupAsync(FakeTrainer trainer, VirtualShiftingModel model)
    {
        var upstream = new DirConClient();
        await upstream.ConnectAsync("127.0.0.1", trainer.Port);
        var proxy = new DirConTrainerProxy(upstream, model, name: "NativeTest");
        await proxy.StartAsync();
        var app = new DirConClient();
        await app.ConnectAsync("127.0.0.1", proxy.Port);
        return (upstream, proxy, app);
    }

    private static async Task Until(Func<bool> cond, int ms = 4000)
    {
        var end = Environment.TickCount64 + ms;
        while (!cond()) { if (Environment.TickCount64 > end) throw new TimeoutException("condition non atteinte"); await Task.Delay(20); }
    }

    [Fact]
    public async Task Native_mode_becomes_active_when_the_trainer_exposes_the_zwift_service()
    {
        await using var trainer = new FakeTrainer { WithZwiftService = true };
        var (up, proxy, app) = await SetupAsync(trainer, new VirtualShiftingModel());
        await using var _1 = up; await using var _2 = proxy; await using var _3 = app;

        Assert.True(proxy.NativeActive);
        // handshake puis init (le trainer a reçu RideOn + 02 01 en premier)
        Assert.Equal(Convert.ToHexString(ZwiftTrainerCodec.RideOnTrainer), trainer.NativeWritesHex()[0]);
        Assert.Equal("410805", trainer.NativeWritesHex()[1]);
    }

    [Fact]
    public async Task Apps_simulation_goes_to_the_trainer_as_native_frames_and_not_as_ftms()
    {
        await using var trainer = new FakeTrainer { WithZwiftService = true };
        var (up, proxy, app) = await SetupAsync(trainer, new VirtualShiftingModel());
        await using var _1 = up; await using var _2 = proxy; await using var _3 = app;

        var responses = new List<byte[]>();
        app.Notification += (ch, v) => { if (ch == FtmsCodec.ControlPoint) lock (responses) responses.Add(v); };

        await app.WriteAsync(FtmsCodec.ControlPoint, SimClimb);
        string expected = Convert.ToHexString(ZwiftTrainerCodec.EncodeSimulation(new SimulationParams(0, 300, 40, 51), full: false));
        await Until(() => trainer.NativeWritesHex().Contains(expected));          // pente BRUTE de l'appli (3 %), valeurs non réécrites

        await Task.Delay(300);
        Assert.Empty(trainer.ControlPointWrites);                                  // rien n'est passé par la commande FTMS 0x11
        lock (responses) Assert.Equal(new byte[] { 0x80, 0x11, 0x01 }, responses[0]);   // l'appli reçoit quand même sa réponse de succès
    }

    [Fact]
    public async Task Gear_change_sends_the_new_ratio_natively_and_leaves_ftms_alone()
    {
        await using var trainer = new FakeTrainer { WithZwiftService = true };
        var model = new VirtualShiftingModel();
        var (up, proxy, app) = await SetupAsync(trainer, model);
        await using var _1 = up; await using var _2 = proxy; await using var _3 = app;

        await app.WriteAsync(FtmsCodec.ControlPoint, SimFlat);
        model.Gear = 24;
        await proxy.ReapplyAsync();

        string gear = Convert.ToHexString(ZwiftTrainerCodec.EncodePhysical(5.49));
        // Attendre aussi la requête qui suit l'écriture du rapport : elle part juste après, pas dans la même écriture
        await Until(() => { var w = trainer.NativeWritesHex(); int i = w.LastIndexOf(gear); return i >= 0 && i + 1 < w.Count; });
        var writes = trainer.NativeWritesHex();
        Assert.Equal("00088804", writes[writes.LastIndexOf(gear) + 1]);            // suivi de la requête du rapport retenu
        Assert.Empty(trainer.ControlPointWrites);
    }

    [Fact]
    public async Task Power_reaches_the_app_unchanged_and_in_order_during_a_burst_of_gear_changes()
    {
        await using var trainer = new FakeTrainer { WithZwiftService = true };
        var model = new VirtualShiftingModel();
        var (up, proxy, app) = await SetupAsync(trainer, model);
        await using var _1 = up; await using var _2 = proxy; await using var _3 = app;
        Assert.True(proxy.NativeActive);

        var received = new List<(byte[] Data, long Tick)>();
        app.Notification += (ch, v) => { if (ch == FtmsCodec.IndoorBikeData) lock (received) received.Add((v, Environment.TickCount64)); };
        await app.EnableNotificationsAsync(FtmsCodec.IndoorBikeData);
        await app.WriteAsync(FtmsCodec.ControlPoint, SimFlat);

        // 80 mesures (une toutes les 25 ms, puissance croissante 100…179 W) pendant que le rapport change 24 fois, toutes les 15 ms.
        const int samples = 80;
        var burst = Task.Run(async () =>
        {
            for (int i = 0; i < 24; i++)
            {
                model.Gear = 1 + (i % 24);
                await proxy.ReapplyAsync();
                await Task.Delay(15);
            }
        });
        var sent = new List<byte[]>();
        for (int i = 0; i < samples; i++)
        {
            int w = 100 + i;
            byte[] data = [0x44, 0x00, 0x10, 0x0E, 0xA0, 0x00, (byte)(w & 0xFF), (byte)(w >> 8)];   // 36 km/h, 80 tr/min, w watts
            sent.Add(data);
            await trainer.PushRawBikeDataAsync(data);
            await Task.Delay(25);
        }
        await burst;
        await Until(() => { lock (received) return received.Count >= samples; });

        lock (received)
        {
            Assert.Equal(samples, received.Count);
            for (int i = 0; i < samples; i++) Assert.Equal(sent[i], received[i].Data);     // aucun octet modifié, aucun réordonnancement
            Assert.True(FtmsCodec.TryGetPowerW(received[^1].Data, out var last));
            Assert.Equal(100 + samples - 1, last);
            long maxGap = 0;
            for (int i = 1; i < samples; i++) maxGap = Math.Max(maxGap, received[i].Tick - received[i - 1].Tick);
            Assert.True(maxGap < 400, $"silence de {maxGap} ms dans le flux de puissance pendant la rafale");
        }
        Assert.Contains("dernière puissance 179 W", proxy.BikeData.TakeSummary(Environment.TickCount64));
    }

    [Fact]
    public async Task Erg_stays_on_ftms_and_leaving_erg_neutralises_the_ftms_simulation_before_native_resumes()
    {
        await using var trainer = new FakeTrainer { WithZwiftService = true };
        var (up, proxy, app) = await SetupAsync(trainer, new VirtualShiftingModel());
        await using var _1 = up; await using var _2 = proxy; await using var _3 = app;

        await app.WriteAsync(FtmsCodec.ControlPoint, [0x05, 0xC8, 0x00]);          // ERG 200 W : FTMS, comme avant
        await Until(() => trainer.ControlPointWrites.Count == 1);
        Assert.Equal(new byte[] { 0x05, 0xC8, 0x00 }, trainer.ControlPointWrites[0]);

        await app.WriteAsync(FtmsCodec.ControlPoint, SimClimb);                    // retour en simulation
        await Until(() => trainer.ControlPointWrites.Count == 2);
        Assert.True(FtmsCodec.TryParseSimulation(trainer.ControlPointWrites[1], out var neutral));
        Assert.Equal(0, neutral.GradeRaw);                                         // simulation FTMS neutre (pente 0) pour quitter l'ERG
        Assert.Contains(Convert.ToHexString(ZwiftTrainerCodec.EncodeSimulation(new SimulationParams(0, 300, 40, 51), full: false)),
            trainer.NativeWritesHex());                                            // puis la simulation native
    }

    [Fact]
    public async Task Zwift_service_is_never_visible_to_the_app()
    {
        await using var trainer = new FakeTrainer { WithZwiftService = true };
        var (up, proxy, app) = await SetupAsync(trainer, new VirtualShiftingModel());
        await using var _1 = up; await using var _2 = proxy; await using var _3 = app;

        var services = await app.DiscoverServicesAsync();
        Assert.DoesNotContain(ZwiftConstants.CustomService, services);
        Assert.Contains(FtmsCodec.FitnessMachineService, services);
    }

    [Fact]
    public async Task Trainer_without_the_zwift_service_falls_back_to_ftms_scaling()
    {
        await using var trainer = new FakeTrainer();                                // pas de service Zwift
        var (up, proxy, app) = await SetupAsync(trainer, new VirtualShiftingModel { Gear = 18 });
        await using var _1 = up; await using var _2 = proxy; await using var _3 = app;

        Assert.False(proxy.NativeActive);
        await app.WriteAsync(FtmsCodec.ControlPoint, SimFlat);
        await Until(() => trainer.ControlPointWrites.Count == 1);
        Assert.True(FtmsCodec.TryParseSimulation(trainer.ControlPointWrites[0], out var sent));
        Assert.True(sent.CwRaw > 51);                                               // mise à l'échelle FTMS, comme avant
    }

    [Fact]
    public async Task Disabled_native_mode_uses_ftms_even_if_the_service_exists()
    {
        await using var trainer = new FakeTrainer { WithZwiftService = true };
        var upstream = new DirConClient();
        await upstream.ConnectAsync("127.0.0.1", trainer.Port);
        var proxy = new DirConTrainerProxy(upstream, new VirtualShiftingModel { Gear = 18 }, name: "NoNative") { NativeZwiftMode = false };
        await proxy.StartAsync();
        await using var _1 = upstream; await using var _2 = proxy;

        Assert.False(proxy.NativeActive);
        Assert.Empty(trainer.NativeWritesHex());                                    // aucun handshake tenté
    }

    [Fact]
    public async Task A_native_channel_failure_falls_back_to_ftms_scaling()
    {
        await using var trainer = new FakeTrainer { WithZwiftService = true };
        var (up, proxy, app) = await SetupAsync(trainer, new VirtualShiftingModel { Gear = 18 });
        await using var _1 = up; await using var _2 = proxy; await using var _3 = app;
        Assert.True(proxy.NativeActive);

        trainer.RejectNativeWrites = true;                                          // le trainer refuse désormais le canal natif
        await app.WriteAsync(FtmsCodec.ControlPoint, SimFlat);

        await Until(() => trainer.ControlPointWrites.Count >= 1);                  // la simulation repasse par FTMS, mise à l'échelle
        Assert.False(proxy.NativeActive);
        Assert.True(FtmsCodec.TryParseSimulation(trainer.ControlPointWrites[0], out var sent));
        Assert.True(sent.CwRaw > 51);
    }

    [Fact]
    public async Task Switching_native_mode_off_releases_the_trainer_and_returns_to_ftms()
    {
        await using var trainer = new FakeTrainer { WithZwiftService = true };
        var model = new VirtualShiftingModel { Gear = 18 };
        var (up, proxy, app) = await SetupAsync(trainer, model);
        await using var _1 = up; await using var _2 = proxy; await using var _3 = app;

        await app.WriteAsync(FtmsCodec.ControlPoint, SimFlat);
        await Until(() => trainer.NativeWritesHex().Count > 7);

        await proxy.SetNativeModeAsync(false);
        Assert.False(proxy.NativeActive);
        Assert.Equal("042A021000", trainer.NativeWritesHex()[^1]);                  // libération : rapport 0
        await Until(() => trainer.ControlPointWrites.Count >= 1);                   // la résistance repasse par FTMS (mise à l'échelle)
        Assert.True(FtmsCodec.TryParseSimulation(trainer.ControlPointWrites[^1], out var sent));
        Assert.True(sent.CwRaw > 51);
    }

    [Fact]
    public async Task After_a_trainer_drop_the_native_session_is_reopened_and_gear_and_simulation_are_resent()
    {
        await using var trainer = new FakeTrainer { WithZwiftService = true };
        var link = new ReconnectingTrainerLink(async ct =>
        {
            var c = new DirConClient();
            await c.ConnectAsync("127.0.0.1", trainer.Port, ct);
            return c;
        }, initialDelay: TimeSpan.FromMilliseconds(50), maxDelay: TimeSpan.FromMilliseconds(50), silenceTimeout: TimeSpan.FromMinutes(5));
        await link.ConnectAsync();
        var model = new VirtualShiftingModel { Gear = 20 };
        var proxy = new DirConTrainerProxy(link, model, name: "NativeReconn");
        await proxy.StartAsync();
        await using var app = new DirConClient();
        await app.ConnectAsync("127.0.0.1", proxy.Port);
        await using var _1 = link; await using var _2 = proxy;
        Assert.True(proxy.NativeActive);

        await app.WriteAsync(FtmsCodec.ControlPoint, SimClimb);
        string sim = Convert.ToHexString(ZwiftTrainerCodec.EncodeSimulation(new SimulationParams(0, 300, 40, 51), full: false));
        await Until(() => trainer.NativeWritesHex().Contains(sim));
        int rideOnsBefore = trainer.NativeWritesHex().Count(h => h == Convert.ToHexString(ZwiftTrainerCodec.RideOnTrainer));

        trainer.DropAllConnections();
        await Until(() => link.ReconnectCount == 1);
        await Until(() => trainer.NativeWritesHex().Count(h => h == Convert.ToHexString(ZwiftTrainerCodec.RideOnTrainer)) == rideOnsBefore + 1);

        string gear = Convert.ToHexString(ZwiftTrainerCodec.EncodePhysical(4.24));   // rapport 20
        await Until(() => trainer.NativeWritesHex().Count(h => h == gear) >= 2);     // rapport renvoyé après la reconnexion
        await Until(() => trainer.NativeWritesHex().Count(h => h == sim) >= 2);      // et la simulation en cours aussi (envoyée juste après le rapport)
        Assert.True(proxy.NativeActive);
    }
}
