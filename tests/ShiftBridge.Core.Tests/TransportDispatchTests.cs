using System.Collections.Concurrent;
using ShiftBridge.Core.Config;
using ShiftBridge.Core.Input;
using ShiftBridge.Core.Obc;
using ShiftBridge.Platform;
using ShiftBridge.Platform.Obc;

namespace ShiftBridge.Core.Tests;

/// <summary>Sortie OBC en mémoire : enregistre les messages, et peut bloquer ses envois tant qu'on ne la débloque pas.</summary>
internal sealed class FakeTransport(string name, Task? gate = null) : IObcTransport
{
    public ConcurrentQueue<byte[]> Sent { get; } = new();
    public string Name => name;
    public bool HasClient { get; private set; } = true;
    public ObcAppInfo? CurrentAppInfo { get; private set; }
#pragma warning disable CS0067
    public event Action<ObcAppInfo>? AppInfoReceived;
    public event Action<ObcHaptic>? HapticReceived;
    public event Action? ClientsChanged;
    public event Action<string>? Log;
#pragma warning restore CS0067

    /// <summary>Simule une appli qui se connecte et annonce ses boutons (App Info).</summary>
    public void RaiseAppInfo(ObcAppInfo info)
    {
        HasClient = true;
        CurrentAppInfo = info;
        ClientsChanged?.Invoke();
        AppInfoReceived?.Invoke(info);
    }

    /// <summary>Simule la déconnexion de l'appli.</summary>
    public void Disconnect()
    {
        HasClient = false;
        CurrentAppInfo = null;
        ClientsChanged?.Invoke();
    }

    public Task StartAsync(CancellationToken ct = default) => Task.CompletedTask;
    public Task StopAsync() => Task.CompletedTask;
    public void SetBattery(int? percent) { }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public async Task SendButtonStateAsync(byte[] message)
    {
        if (gate is not null) await gate;
        Sent.Enqueue(message);
    }
}

public class TransportDispatchTests
{
    private static async Task Until(Func<bool> cond, int ms = 4000)
    {
        var end = Environment.TickCount64 + ms;
        while (!cond()) { if (Environment.TickCount64 > end) throw new TimeoutException(); await Task.Delay(10); }
    }

    [Fact]
    public async Task A_blocked_transport_does_not_delay_the_others()
    {
        var release = new TaskCompletionSource();
        var blocked = new FakeTransport("bloqué", release.Task);
        var fast = new FakeTransport("rapide");
        await using var bridge = new BridgeService(new AppConfig());
        await bridge.AddTransportAsync(blocked);
        await bridge.AddTransportAsync(fast);

        bridge.InjectEvents([new ButtonEvent(PhysicalButton.RightB, true)]);
        bridge.InjectEvents([new ButtonEvent(PhysicalButton.RightB, false)]);

        // Le transport rapide reçoit tout alors que le premier n'a encore rien envoyé.
        await Until(() => fast.Sent.Count == 2);
        Assert.Empty(blocked.Sent);

        release.SetResult();
        await Until(() => blocked.Sent.Count == 2);
        // L'ordre est conservé sur chaque transport : appuyé (1) puis relâché (0).
        Assert.Equal([(byte)1, (byte)0], blocked.Sent.Select(m => m[2]).ToArray());
        Assert.Equal([(byte)1, (byte)0], fast.Sent.Select(m => m[2]).ToArray());
    }

    [Fact]
    public async Task A_removed_transport_stops_receiving_messages()
    {
        var gone = new FakeTransport("retiré");
        var kept = new FakeTransport("gardé");
        await using var bridge = new BridgeService(new AppConfig());
        await bridge.AddTransportAsync(gone);
        await bridge.AddTransportAsync(kept);
        await bridge.RemoveTransportAsync(gone);

        bridge.InjectEvents([new ButtonEvent(PhysicalButton.RightB, true)]);
        await Until(() => kept.Sent.Count == 1);
        await Task.Delay(50);
        Assert.Empty(gone.Sent);
    }
}
