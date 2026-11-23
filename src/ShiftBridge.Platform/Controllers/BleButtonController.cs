using ShiftBridge.Core.Controllers;
using ShiftBridge.Core.Controllers.Decoders;
using ShiftBridge.Core.Input;
using ShiftBridge.Core.Localization;
using ShiftBridge.Core.Trainer;
using ShiftBridge.Platform.Ble;

namespace ShiftBridge.Platform.Controllers;

/// <summary>Ce qui distingue une manette BLE d'une autre : où elle notifie et comment lire ses trames.</summary>
public sealed record ControllerSpec(
    ControllerKind Kind, Guid Service, Guid Characteristic, BleNotifyMode Mode, Func<IButtonDecoder> CreateDecoder)
{
    public static ControllerSpec? For(ControllerKind kind) => kind switch
    {
        ControllerKind.ThinkriderVs200 => new(kind, ThinkriderVs200Decoder.Service, ThinkriderVs200Decoder.Notify, BleNotifyMode.Notify, () => new ThinkriderVs200Decoder()),
        ControllerKind.CycplusBc2 => new(kind, BleUuids.NordicUartService, BleUuids.NordicUartTx, BleNotifyMode.Notify, () => new CycplusBc2Decoder()),
        ControllerKind.OnelapKey => new(kind, OnelapKeyDecoder.Service, OnelapKeyDecoder.Notify, BleNotifyMode.Notify, () => new OnelapKeyDecoder()),
        _ => null,
    };
}

/// <summary>
/// Manette BLE à une seule caractéristique de notification (VS200, BC2, Onelap Key…), sans handshake ni vibration.
/// Même cycle de vie que <see cref="ZwiftController"/> : reconnexion progressive (1 → 15 s), notifications abonnées après la pose
/// du gestionnaire, touches relâchées à chaque coupure. Les appuis « instantanés » (sans trame de relâchement) sont émis en
/// appui puis relâchement différé, sérialisés pour que deux appuis rapprochés ne se confondent pas côté appli.
/// </summary>
public sealed class BleButtonController : IController
{
    private static readonly Guid BatteryService = FtmsCodec.BaseUuid(0x180F);
    private static readonly Guid BatteryLevel = FtmsCodec.BaseUuid(0x2A19);

    private readonly IBleCentral _central;
    private readonly ControllerSpec _spec;
    private readonly ButtonStateTracker _tracker = new();
    private readonly object _trackerLock = new();
    private readonly SemaphoreSlim _tapGate = new(1, 1);
    private IButtonDecoder _decoder;
    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private IBleConnection? _connection;
    private TaskCompletionSource? _disconnected;
    private long _frameCount;

    public BleButtonController(IBleCentral central, DiscoveredController info, ControllerSpec spec, bool autoReconnect = true)
    {
        _central = central;
        Info = info;
        _spec = spec;
        _decoder = spec.CreateDecoder();
        AutoReconnect = autoReconnect;
    }

    public DiscoveredController Info { get; }
    public bool AutoReconnect { get; set; }
    public bool VerboseFrames { get; set; }

    /// <summary>Durée pendant laquelle un appui instantané reste « enfoncé » avant d'être relâché.</summary>
    public int TapHoldMs { get; set; } = 60;

    public ControllerState State { get; private set; } = ControllerState.Disconnected;
    public int? BatteryPercent { get; private set; }
    public bool CanVibrate => false;

    public event Action<ControllerState>? StateChanged;
    public event Action<IReadOnlyList<ButtonEvent>>? ButtonEvents;
    public event Action<int>? BatteryChanged;
    public event Action<string>? Frame;
    public event Action<string>? Log;

    public void Start()
    {
        if (_runTask is { IsCompleted: false }) return;
        _cts = new CancellationTokenSource();
        _runTask = RunAsync(_cts.Token);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_runTask is not null) { try { await _runTask; } catch { /* annulé */ } }
        await CleanupAsync();
        ReleaseAllButtons(); // une touche tenue pendant « Déconnecter » ne doit pas rester appuyée côté appli
        SetState(ControllerState.Disconnected);
    }

    public Task VibrateAsync(byte pattern = 0x20) => Task.CompletedTask;

    private async Task RunAsync(CancellationToken ct)
    {
        int delayMs = 1000;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                SetState(ControllerState.Connecting);
                await ConnectOnceAsync(ct);
                delayMs = 1000;
                await _disconnected!.Task.WaitAsync(ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                Log?.Invoke($"{Info.Name} : {ex.Message}");
                SetState(ControllerState.Error);
            }

            ReleaseAllButtons();
            await CleanupAsync();
            if (!AutoReconnect || ct.IsCancellationRequested) break;
            SetState(ControllerState.Disconnected);

            try { await Task.Delay(delayMs, ct); } catch (OperationCanceledException) { break; }
            delayMs = Math.Min(delayMs * 2, 15000); // 1 s, 2 s, 4 s … 15 s
        }
        SetState(ControllerState.Disconnected);
    }

    private async Task ConnectOnceAsync(CancellationToken ct)
    {
        _disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _decoder = _spec.CreateDecoder(); // état de trame remis à zéro à chaque liaison

        try { _connection = await _central.ConnectAsync(Info.Address, new BleConnectOptions(), ct); }
        catch (BleDeviceNotFoundException) { throw new IOException(Strings.Err_ControllerNotFound); }
        _connection.Disconnected += OnDisconnected;
        _connection.ValueChanged += OnValueChanged;
        _connection.Log += m => Log?.Invoke(m);

        var services = await _connection.DiscoverAsync(uuid => uuid == _spec.Service, strict: true, ct: ct);
        var service = services.FirstOrDefault(s => s.Uuid == _spec.Service);
        if (service is null || service.Characteristics.All(c => c.Uuid != _spec.Characteristic))
            throw new IOException(Strings.Err_ControllerServiceMissing);

        // Le gestionnaire ValueChanged est posé AVANT d'activer les notifications pour ne perdre aucune trame.
        await _connection.SetNotifyAsync(_spec.Service, _spec.Characteristic, _spec.Mode);
        Log?.Invoke($"{Info.Name} : notifications activées");

        _ = ReadBatteryAsync(); // best-effort
        SetState(ControllerState.Connected);
    }

    private async Task ReadBatteryAsync()
    {
        try
        {
            var connection = _connection;
            if (connection is null) return;
            var found = await connection.DiscoverAsync(uuid => uuid == BatteryService);
            if (found.Count == 0) return;
            byte[] b = await connection.ReadAsync(BatteryService, BatteryLevel);
            if (b.Length > 0) { BatteryPercent = b[0]; BatteryChanged?.Invoke(b[0]); }
        }
        catch { /* la batterie n'est pas critique */ }
    }

    private void OnValueChanged(Guid service, Guid characteristic, byte[] data)
    {
        if (characteristic != _spec.Characteristic) return;
        _frameCount++;
        string hex = Convert.ToHexString(data);
        DecodedFrame? frame;
        try { frame = _decoder.Decode(characteristic, data); }
        catch (Exception ex) { Log?.Invoke($"{Info.Name} : trame illisible ({ex.Message}) {hex}"); return; }

        if (frame is null)
        {
            Frame?.Invoke($"{Info.Name} < {hex} (non reconnue)");
            return;
        }

        List<ButtonEvent> events = [];
        if (frame.Held is { } held)
        {
            var keys = held.Select(n => ButtonKey.Controller(Info.Kind, n)).ToHashSet();
            lock (_trackerLock) events = _tracker.Update(keys);
        }
        var taps = frame.Taps.Select(n => ButtonKey.Controller(Info.Kind, n)).ToList();

        if (VerboseFrames || events.Count > 0 || taps.Count > 0 || _frameCount == 1)
            Frame?.Invoke($"{Info.Name} < {hex}");

        if (frame.BatteryPercent is { } pct) { BatteryPercent = pct; BatteryChanged?.Invoke(pct); }
        if (events.Count > 0) ButtonEvents?.Invoke(events);
        if (taps.Count > 0) _ = EmitTapsAsync(taps);
    }

    /// <summary>Appui puis relâchement différé de chaque touche, l'un après l'autre (sans trame de relâchement côté manette).</summary>
    private async Task EmitTapsAsync(List<ButtonKey> taps)
    {
        await _tapGate.WaitAsync();
        try
        {
            foreach (var key in taps)
            {
                ButtonEvents?.Invoke([new ButtonEvent(key, true)]);
                if (TapHoldMs > 0) await Task.Delay(TapHoldMs);
                ButtonEvents?.Invoke([new ButtonEvent(key, false)]);
            }
        }
        catch (Exception ex) { Log?.Invoke($"{Info.Name} : appui impossible ({ex.Message})"); }
        finally { _tapGate.Release(); }
    }

    private void OnDisconnected()
    {
        Log?.Invoke($"{Info.Name} : déconnectée");
        _disconnected?.TrySetResult();
    }

    private void ReleaseAllButtons()
    {
        List<ButtonEvent> events;
        lock (_trackerLock) events = _tracker.Reset();
        if (events.Count > 0) ButtonEvents?.Invoke(events);
    }

    private void SetState(ControllerState s)
    {
        if (State == s) return;
        State = s;
        StateChanged?.Invoke(s);
    }

    private async Task CleanupAsync()
    {
        var c = _connection;
        _connection = null;
        if (c is null) return;
        c.Disconnected -= OnDisconnected;
        c.ValueChanged -= OnValueChanged;
        try { await c.DisposeAsync(); } catch { /* déjà fermée */ }
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
