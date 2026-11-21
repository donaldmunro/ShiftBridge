using ShiftBridge.Core.Trainer;

namespace ShiftBridge.Core.Tests;

public class FtmsControlTrackerTests
{
    private static readonly byte[] Sim = [0x11, 0, 0, 0x2C, 0x01, 40, 51];
    private static readonly byte[] Power = [0x05, 0xC8, 0x00]; // 200 W

    [Fact]
    public void Reapply_is_allowed_only_while_the_last_target_is_a_simulation()
    {
        var t = new FtmsControlTracker();
        Assert.False(t.CanReapplySimulation);

        t.OnAppCommand([FtmsCodec.OpRequestControl]);
        t.OnAppCommand(Sim);
        Assert.True(t.CanReapplySimulation);

        // L'appli passe en ERG : un changement de rapport ne doit plus toucher au trainer.
        t.OnAppCommand(Power);
        Assert.False(t.CanReapplySimulation);

        t.OnAppCommand(Sim);
        Assert.True(t.CanReapplySimulation);
    }

    [Fact]
    public void Replay_sequence_follows_what_the_app_sent_in_order()
    {
        var t = new FtmsControlTracker();
        t.OnAppCommand([FtmsCodec.OpRequestControl]);
        t.OnAppCommand([FtmsCodec.OpStart]);
        t.OnAppCommand(Sim);

        var seq = t.ReplaySequence(cp => FtmsCodec.WithGrade(cp, 777));
        Assert.Equal(3, seq.Count);
        Assert.Equal(FtmsCodec.OpRequestControl, seq[0][0]);
        Assert.Equal(FtmsCodec.OpStart, seq[1][0]);
        Assert.True(FtmsCodec.TryParseSimulation(seq[2], out var p));
        Assert.Equal(777, p.GradeRaw);                         // la simulation est ré-ajustée au rapport courant
    }

    [Fact]
    public void Replay_keeps_erg_target_untouched_and_never_replays_reset()
    {
        var t = new FtmsControlTracker();
        t.OnAppCommand([FtmsCodec.OpRequestControl]);
        t.OnAppCommand(Power);
        var seq = t.ReplaySequence(_ => throw new InvalidOperationException("pas d'ajustement hors simulation"));
        Assert.Equal(Power, seq[^1]);

        t.OnAppCommand([FtmsCodec.OpReset]);
        Assert.DoesNotContain(t.ReplaySequence(c => c), s => s[0] == FtmsCodec.OpReset);
        Assert.Null(t.LastTarget);
    }

    [Fact]
    public void Stop_prevents_replaying_start()
    {
        var t = new FtmsControlTracker();
        t.OnAppCommand([FtmsCodec.OpRequestControl]);
        t.OnAppCommand([FtmsCodec.OpStart]);
        t.OnAppCommand([FtmsCodec.OpStop, 0x01]);
        Assert.DoesNotContain(t.ReplaySequence(c => c), s => s[0] == FtmsCodec.OpStart);
    }
}

public class ControlPointArbiterTests
{
    private sealed class Rig : IDisposable
    {
        public readonly List<byte[]> Sent = [];
        public readonly List<byte[]> ToApp = [];
        public readonly List<byte[]> Foreign = [];
        public Func<byte[], Task>? SendHook;
        public readonly ControlPointArbiter Arbiter;

        public Rig(TimeSpan? gap = null, TimeSpan? timeout = null)
        {
            Arbiter = new ControlPointArbiter(async (cp, _) =>
            {
                lock (Sent) Sent.Add(cp);
                if (SendHook is not null) await SendHook(cp);
            }, gap ?? TimeSpan.FromMilliseconds(60), timeout ?? TimeSpan.FromMilliseconds(400));
            Arbiter.ResponseForApp += r => { lock (ToApp) ToApp.Add(r); };
            Arbiter.ForeignResponse += r => { lock (Foreign) Foreign.Add(r); };
        }

        public int SentCount { get { lock (Sent) return Sent.Count; } }
        public void Dispose() => Arbiter.Dispose();
    }

    private static async Task Until(Func<bool> cond, int ms = 3000)
    {
        var end = Environment.TickCount64 + ms;
        while (!cond()) { if (Environment.TickCount64 > end) throw new TimeoutException(); await Task.Delay(10); }
    }

    private static byte[] Ok(byte op) => [0x80, op, 0x01];

    [Fact]
    public async Task Only_one_command_is_in_flight_until_its_response_arrives()
    {
        using var rig = new Rig();
        var first = rig.Arbiter.SubmitFromAppAsync([0x00]);
        var second = rig.Arbiter.SubmitFromAppAsync([0x07]);

        await Until(() => rig.SentCount == 1);
        await Task.Delay(100);
        Assert.Equal(1, rig.SentCount);                         // la 2e attend la réponse à la 1re
        Assert.True(await first);

        rig.Arbiter.OnResponse(Ok(0x00));
        await Until(() => rig.SentCount == 2);
        Assert.Equal(new byte[] { 0x07 }, rig.Sent[1]);
        Assert.True(await second);
    }

    [Fact]
    public async Task Response_that_arrives_before_the_write_returns_is_still_matched()
    {
        using var rig = new Rig();
        var release = new TaskCompletionSource();
        rig.SendHook = async cp => { rig.Arbiter.OnResponse(Ok(cp[0])); await release.Task; }; // indication AVANT le retour de l'écriture

        _ = rig.Arbiter.SubmitFromAppAsync([0x11, 0, 0, 0, 0, 40, 51]);
        await Until(() => rig.SentCount == 1);
        await Until(() => { lock (rig.ToApp) return rig.ToApp.Count == 1; });
        Assert.Empty(rig.Foreign);
        release.SetResult();
    }

    [Fact]
    public async Task Proxy_response_is_swallowed_and_app_response_is_forwarded()
    {
        using var rig = new Rig();
        rig.Arbiter.SubmitFromProxy([0x11, 0, 0, 0x10, 0x00, 40, 51]);
        await Until(() => rig.SentCount == 1);
        rig.Arbiter.OnResponse(Ok(0x11));
        await Task.Delay(50);
        lock (rig.ToApp) Assert.Empty(rig.ToApp);                // réponse à une commande du proxy : jamais transmise à l'appli

        _ = rig.Arbiter.SubmitFromAppAsync([0x05, 0xC8, 0x00]);
        await Until(() => rig.SentCount == 2);
        rig.Arbiter.OnResponse(Ok(0x05));
        await Until(() => { lock (rig.ToApp) return rig.ToApp.Count == 1; });
    }

    [Fact]
    public async Task Burst_of_proxy_reapplies_is_coalesced_to_the_latest_one()
    {
        using var rig = new Rig(gap: TimeSpan.FromMilliseconds(200));
        for (short g = 1; g <= 8; g++) rig.Arbiter.SubmitFromProxy([0x11, 0, 0, (byte)g, 0, 40, 51]);

        await Until(() => rig.SentCount >= 1);
        rig.Arbiter.OnResponse(Ok(0x11));
        await Task.Delay(500);
        // La première part aussitôt ; le reste de la rafale est fusionné : jamais 8 envois.
        Assert.InRange(rig.SentCount, 1, 2);
        Assert.Equal(8, rig.Sent[^1][3]);                        // et c'est bien la dernière valeur qui part
    }

    [Fact]
    public async Task App_command_has_priority_over_a_waiting_proxy_reapply()
    {
        using var rig = new Rig(gap: TimeSpan.FromMilliseconds(500));
        rig.Arbiter.SubmitFromProxy([0x11, 0, 0, 1, 0, 40, 51]);
        await Until(() => rig.SentCount == 1);
        rig.Arbiter.OnResponse(Ok(0x11));

        rig.Arbiter.SubmitFromProxy([0x11, 0, 0, 2, 0, 40, 51]);   // doit attendre l'écart minimal…
        _ = rig.Arbiter.SubmitFromAppAsync([0x04, 0x0A, 0x00]);      // …mais l'appli passe devant, sans attendre 500 ms
        await Until(() => rig.SentCount == 2, 300);
        Assert.Equal(0x04, rig.Sent[1][0]);
    }

    [Fact]
    public async Task New_app_target_cancels_a_pending_proxy_reapply()
    {
        using var rig = new Rig(gap: TimeSpan.FromMilliseconds(400));
        rig.Arbiter.SubmitFromProxy([0x11, 0, 0, 1, 0, 40, 51]);
        await Until(() => rig.SentCount == 1);
        rig.Arbiter.OnResponse(Ok(0x11));
        rig.Arbiter.SubmitFromProxy([0x11, 0, 0, 9, 0, 40, 51]);   // en attente

        _ = rig.Arbiter.SubmitFromAppAsync([0x05, 0xC8, 0x00]);     // l'appli passe en ERG : l'ancienne simulation n'a plus lieu d'être
        await Until(() => rig.SentCount == 2);
        rig.Arbiter.OnResponse(Ok(0x05));
        await Task.Delay(600);
        Assert.Equal(2, rig.SentCount);
    }

    [Fact]
    public async Task Response_without_command_in_flight_is_reported_as_foreign()
    {
        using var rig = new Rig();
        rig.Arbiter.OnResponse(Ok(0x11));
        lock (rig.Foreign) Assert.Single(rig.Foreign);
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Silent_trainer_times_out_and_does_not_block_following_commands()
    {
        using var rig = new Rig(timeout: TimeSpan.FromMilliseconds(150));
        byte? timedOut = null;
        rig.Arbiter.Timeout += op => timedOut = op;

        _ = rig.Arbiter.SubmitFromAppAsync([0x00]);
        _ = rig.Arbiter.SubmitFromAppAsync([0x07]);
        await Until(() => rig.SentCount == 2, 2000);             // la 2e part après le délai, sans réponse à la 1re
        Assert.Equal((byte)0x00, timedOut);
    }

    [Fact]
    public async Task Replay_sequence_is_sent_in_order_and_before_queued_app_commands()
    {
        using var rig = new Rig();
        rig.Arbiter.SubmitProxySequence([[0x00], [0x07], [0x05, 0xC8, 0x00]]);
        _ = rig.Arbiter.SubmitFromAppAsync([0x04, 0x01, 0x00]);

        foreach (byte op in new byte[] { 0x00, 0x07, 0x05 })
        {
            await Until(() => rig.SentCount >= 1 && rig.Sent[^1][0] == op && rig.SentCount == Array.IndexOf(new byte[] { 0x00, 0x07, 0x05 }, op) + 1);
            rig.Arbiter.OnResponse(Ok(op));
        }
        await Until(() => rig.SentCount == 4);
        Assert.Equal(0x04, rig.Sent[3][0]);
    }
}

public class FtmsFeaturesAndPolicyTests
{
    [Fact]
    public void Feature_bits_are_decoded()
    {
        // fonctions : cadence (bit 1) + puissance (bit 14) ; cibles : résistance (2), puissance (3), simulation (13)
        uint machine = (1u << 1) | (1u << 14);
        uint targets = (1u << 2) | (1u << 3) | (1u << 13);
        var data = BitConverter.GetBytes(machine).Concat(BitConverter.GetBytes(targets)).ToArray();

        Assert.True(FtmsFeatures.TryParse(data, out var f));
        Assert.True(f.SupportsIndoorBikeSimulation);
        Assert.True(f.SupportsPowerTarget);
        Assert.True(f.HasCadence);
        Assert.Contains("simulation=oui", f.Describe());
    }

    [Fact]
    public void Trainer_without_simulation_bit_is_detected_and_short_data_rejected()
    {
        var data = BitConverter.GetBytes(0u).Concat(BitConverter.GetBytes(1u << 3)).ToArray();
        Assert.True(FtmsFeatures.TryParse(data, out var f));
        Assert.False(f.SupportsIndoorBikeSimulation);
        Assert.False(FtmsFeatures.TryParse(new byte[4], out _));
    }

    [Fact]
    public void Allow_list_keeps_ftms_and_filters_vendor_control_inside_cycling_power()
    {
        var p = new GattRelayPolicy(full: false);
        var cps = FtmsCodec.BaseUuid(0x1818);
        Assert.True(p.ShouldRelayService(FtmsCodec.FitnessMachineService));
        Assert.True(p.ShouldRelayService(cps));
        Assert.False(p.ShouldRelayService(FtmsCodec.BaseUuid(0x1800)));
        Assert.False(p.ShouldRelayService(new Guid("a026ee01-0a7d-4ab3-97fa-f1500f9feb8b")));      // service constructeur

        Assert.True(p.ShouldRelayCharacteristic(cps, FtmsCodec.BaseUuid(0x2A63)));
        Assert.False(p.ShouldRelayCharacteristic(cps, new Guid("a026e005-0a7d-4ab3-97fa-f1500f9feb8b"))); // Wahoo : dans 0x1818
        Assert.False(p.ShouldRelayCharacteristic(cps, FtmsCodec.BaseUuid(0x2A66)));                       // Cycling Power Control Point
        Assert.True(p.ShouldRelayCharacteristic(FtmsCodec.FitnessMachineService, FtmsCodec.ControlPoint));
    }

    [Fact]
    public void Full_mode_relays_everything()
    {
        var p = new GattRelayPolicy(full: true);
        Assert.True(p.ShouldRelayService(new Guid("a026ee01-0a7d-4ab3-97fa-f1500f9feb8b")));
        Assert.True(p.ShouldRelayCharacteristic(FtmsCodec.BaseUuid(0x1818), new Guid("a026e005-0a7d-4ab3-97fa-f1500f9feb8b")));
    }

    [Theory]
    [InlineData(0x02u, (byte)0x01)]            // Read
    [InlineData(0x08u, (byte)0x02)]            // Write
    [InlineData(0x04u, (byte)0x02)]            // Write sans réponse
    [InlineData(0x10u, (byte)0x04)]            // Notify
    [InlineData(0x20u, (byte)0x08)]            // Indicate
    [InlineData(0x28u, (byte)0x0A)]            // Write + Indicate (Control Point FTMS)
    public void Gatt_properties_map_to_dircon_bits(uint gatt, byte expected) =>
        Assert.Equal(expected, GattRelayPolicy.GattToDirConProps(gatt));

    [Fact]
    public void Status_simulation_grade_is_rewritten_back_to_the_apps_grade()
    {
        // statut 0x12 : [0x12, vent(2), pente(2), crr, cw] avec la pente réécrite par le proxy (0x0190 = 4,00 %)
        byte[] status = [0x12, 0x00, 0x00, 0x90, 0x01, 40, 51];
        var back = FtmsCodec.RewriteStatusGrade(status, 0); // l'appli avait demandé 0 %
        Assert.Equal(new byte[] { 0x12, 0x00, 0x00, 0x00, 0x00, 40, 51 }, back);
        // Un autre statut (ou une trame trop courte) est renvoyé tel quel.
        Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, FtmsCodec.RewriteStatusGrade([0x01, 0x02, 0x03], 5));
        Assert.Equal(new byte[] { 0x12, 0x00 }, FtmsCodec.RewriteStatusGrade([0x12, 0x00], 5));
    }
}
