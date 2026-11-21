using ShiftBridge.Core.DirCon;
using ShiftBridge.Core.Trainer;
using ShiftBridge.Platform.Trainer;

namespace ShiftBridge.Core.Tests;

/// <summary>Liaison trainer en mémoire : arbre FTMS minimal, notifications injectables, coupure simulable.</summary>
internal sealed class FakeLink : ITrainerLink
{
    public int DiscoverCalls;
    public List<Guid> Subscribed = [];
    public List<(Guid, byte[])> Writes = [];
    public bool Disposed;
    private bool _connected = true;

    public bool IsConnected => _connected && !Disposed;
    public event Action<Guid, byte[]>? Notification;
    public event Action? Disconnected;
#pragma warning disable CS0067
    public event Action<string>? Log;
#pragma warning restore CS0067

    public Task<List<Guid>> DiscoverServicesAsync() { DiscoverCalls++; return Task.FromResult(new List<Guid> { FtmsCodec.FitnessMachineService }); }
    public Task<List<(Guid Uuid, byte Props)>> DiscoverCharacteristicsAsync(Guid service)
    {
        DiscoverCalls++;
        return Task.FromResult(new List<(Guid, byte)>
        {
            (FtmsCodec.IndoorBikeData, DirConProps.Notify), (FtmsCodec.ControlPoint, DirConProps.Write | DirConProps.Indicate),
        });
    }
    public Task<byte[]> ReadAsync(Guid ch) => Task.FromResult(new byte[] { 1, 2, 3 });
    public Task WriteAsync(Guid ch, byte[] value) { Writes.Add((ch, value)); return Task.CompletedTask; }
    public Task EnableNotificationsAsync(Guid ch, bool enable = true) { if (enable) Subscribed.Add(ch); return Task.CompletedTask; }

    public void Push(Guid ch, byte[] v) => Notification?.Invoke(ch, v);
    public void Drop() { _connected = false; Disconnected?.Invoke(); }
    public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
}

public class ReconnectingLinkTests
{
    private static async Task Until(Func<bool> cond, int ms = 4000)
    {
        var end = Environment.TickCount64 + ms;
        while (!cond()) { if (Environment.TickCount64 > end) throw new TimeoutException(); await Task.Delay(10); }
    }

    private static ReconnectingTrainerLink Make(List<FakeLink> created, Func<int, bool>? fail = null, TimeSpan? silence = null)
    {
        int attempt = 0;
        return new ReconnectingTrainerLink(_ =>
        {
            int n = Interlocked.Increment(ref attempt);
            if (fail?.Invoke(n) == true) throw new IOException("trainer injoignable");
            var l = new FakeLink();
            lock (created) created.Add(l);
            return Task.FromResult<ITrainerLink>(l);
        }, initialDelay: TimeSpan.FromMilliseconds(20), maxDelay: TimeSpan.FromMilliseconds(80), silenceTimeout: silence ?? TimeSpan.FromMinutes(5));
    }

    [Fact]
    public async Task Dispose_while_the_factory_is_connecting_releases_the_new_link()
    {
        var links = new List<FakeLink>();
        var gate = new TaskCompletionSource();
        int attempt = 0;
        var link = new ReconnectingTrainerLink(async _ =>
        {
            if (Interlocked.Increment(ref attempt) > 1) await gate.Task; // la reconnexion reste « en cours de connexion »
            var l = new FakeLink();
            lock (links) links.Add(l);
            return l;
        }, initialDelay: TimeSpan.FromMilliseconds(10), maxDelay: TimeSpan.FromMilliseconds(20), silenceTimeout: TimeSpan.FromMinutes(5));
        await link.ConnectAsync();

        links[0].Drop();
        await Until(() => Volatile.Read(ref attempt) >= 2);
        await link.DisposeAsync();   // arrêt pendant que la fabrique connecte
        gate.SetResult();            // la connexion aboutit APRÈS l'arrêt

        await Until(() => links.Count == 2);
        await Until(() => links[1].Disposed); // sans le correctif, elle restait ouverte (trainer « occupé »)
    }

    [Fact]
    public async Task Drop_triggers_reconnect_restores_subscriptions_and_keeps_the_service_tree()
    {
        var links = new List<FakeLink>();
        await using var link = Make(links);
        await link.ConnectAsync();

        var services = await link.DiscoverServicesAsync();
        var chars = await link.DiscoverCharacteristicsAsync(services[0]);
        await link.EnableNotificationsAsync(FtmsCodec.IndoorBikeData);
        await link.EnableNotificationsAsync(FtmsCodec.ControlPoint);
        int dropped = 0, reconnected = 0;
        link.Dropped += () => dropped++;
        link.Reconnected += () => reconnected++;

        links[0].Drop();
        await Until(() => reconnected == 1);

        Assert.Equal(1, dropped);
        Assert.Equal(1, link.ReconnectCount);
        Assert.Equal(2, links.Count);
        Assert.Equal(LinkState.Connected, link.State);
        Assert.Contains(FtmsCodec.IndoorBikeData, links[1].Subscribed);   // abonnements rétablis sur la nouvelle liaison
        Assert.Contains(FtmsCodec.ControlPoint, links[1].Subscribed);
        Assert.Equal(0, links[1].DiscoverCalls);                           // l'arbre n'est PAS redécouvert : figé à la 1re connexion
        Assert.Equal(services, await link.DiscoverServicesAsync());
        Assert.Equal(chars, await link.DiscoverCharacteristicsAsync(services[0]));
        Assert.True(links[0].Disposed);
    }

    [Fact]
    public async Task Notifications_from_the_new_link_are_forwarded_and_the_old_one_is_ignored()
    {
        var links = new List<FakeLink>();
        await using var link = Make(links);
        await link.ConnectAsync();
        var seen = new List<byte[]>();
        link.Notification += (_, v) => { lock (seen) seen.Add(v); };

        links[0].Push(FtmsCodec.IndoorBikeData, [1]);
        links[0].Drop();
        await Until(() => links.Count == 2 && link.State == LinkState.Connected);
        links[0].Push(FtmsCodec.IndoorBikeData, [2]);                       // ancienne liaison : ignorée
        links[1].Push(FtmsCodec.IndoorBikeData, [3]);

        lock (seen) Assert.Equal([new byte[] { 1 }, new byte[] { 3 }], seen);
    }

    [Fact]
    public async Task Reconnect_retries_with_backoff_until_the_trainer_is_back()
    {
        var links = new List<FakeLink>();
        await using var link = Make(links, fail: n => n is >= 2 and <= 4);   // 1re connexion OK, puis 3 échecs
        await link.ConnectAsync();
        var logs = new List<string>();
        link.Log += m => { lock (logs) logs.Add(m); };

        links[0].Drop();
        await Until(() => link.ReconnectCount == 1);
        lock (logs) Assert.Equal(3, logs.Count(l => l.Contains("Reconnexion au trainer impossible")));
    }

    [Fact]
    public async Task Silent_link_is_declared_dead_by_the_watchdog()
    {
        var links = new List<FakeLink>();
        await using var link = Make(links, silence: TimeSpan.FromMilliseconds(300));
        await link.ConnectAsync();
        await link.EnableNotificationsAsync(FtmsCodec.IndoorBikeData);

        await Until(() => link.ReconnectCount >= 1, 3000);                   // aucune notification : liaison figée → reconnexion
        Assert.True(links[0].Disposed);
    }

    [Fact]
    public async Task Watchdog_stays_quiet_while_notifications_keep_arriving()
    {
        var links = new List<FakeLink>();
        await using var link = Make(links, silence: TimeSpan.FromMilliseconds(400));
        await link.ConnectAsync();
        await link.EnableNotificationsAsync(FtmsCodec.IndoorBikeData);

        for (int i = 0; i < 12; i++) { links[0].Push(FtmsCodec.IndoorBikeData, [1]); await Task.Delay(100); }
        Assert.Equal(0, link.ReconnectCount);
    }

    [Fact]
    public async Task Calls_during_an_outage_fail_cleanly_and_subscriptions_requested_meanwhile_are_applied_later()
    {
        var links = new List<FakeLink>();
        await using var link = Make(links, fail: n => n == 2);               // la 1re reconnexion échoue : fenêtre de coupure
        await link.ConnectAsync();
        links[0].Drop();
        await Until(() => link.State == LinkState.Reconnecting);

        await Assert.ThrowsAsync<IOException>(() => link.WriteAsync(FtmsCodec.ControlPoint, [0x00]));
        await link.EnableNotificationsAsync(FtmsCodec.FitnessMachineStatus);  // demandé pendant la coupure : mémorisé

        await Until(() => link.ReconnectCount == 1);
        Assert.Contains(FtmsCodec.FitnessMachineStatus, links[^1].Subscribed);
    }

    [Fact]
    public async Task First_connection_failure_is_reported_to_the_caller()
    {
        var links = new List<FakeLink>();
        await using var link = Make(links, fail: n => true);
        await Assert.ThrowsAsync<IOException>(() => link.ConnectAsync());
        Assert.Equal(LinkState.Failed, link.State);
    }
}
