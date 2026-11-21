using ShiftBridge.Core.Localization;
using ShiftBridge.Core.Input;
using ShiftBridge.Core.Trainer;
using ShiftBridge.Core.Zwift;
using ShiftBridge.Core.Zwift.Parsers;
using ShiftBridge.Platform.Ble;

namespace ShiftBridge.Platform.Controllers;

public enum ControllerState { Disconnected, Connecting, Connected, Locked, Error }

/// <summary>
/// Connexion à une manette Zwift : abonnements Async/SyncTx, handshake « RideOn » (non chiffré),
/// reconnexion automatique, batterie et vibration. Une seule instance par manette physique.
/// L'accès GATT passe par <see cref="IBleConnection"/> (WinRT sous Windows, Android.Bluetooth sous Android).
/// </summary>
public sealed class ZwiftController : IAsyncDisposable
{
    private static readonly Guid BatteryService = FtmsCodec.BaseUuid(0x180F);
    private static readonly Guid BatteryLevel = FtmsCodec.BaseUuid(0x2A19);

    private readonly IBleCentral _central;
    private readonly ButtonStateTracker _tracker = new();
    private readonly object _trackerLock = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private CancellationTokenSource? _cts;
    private Task? _runTask;
    private IBleConnection? _connection;
    private Guid _service;
    private long _lastFrameTick;
    private TaskCompletionSource? _disconnected;

    public ZwiftController(IBleCentral central, DiscoveredZwiftDevice info, bool autoReconnect = true)
    {
        _central = central;
        Info = info;
        AutoReconnect = autoReconnect;
    }

    public DiscoveredZwiftDevice Info { get; }
    public bool AutoReconnect { get; set; }

    /// <summary>Si vrai, une manette qui se verrouille (Click v2 gauche) est reconnectée aussitôt.</summary>
    public bool ReconnectOnLock { get; set; } = true;

    /// <summary>Journalise toutes les trames de boutons, même au repos (capture détaillée).</summary>
    public bool VerboseFrames { get; set; }

    private long _lastVibrationTick;
    private long _lastStatTick;
    private long _buttonFrameCount;
    private long _countAtLastStat;

    public ControllerState State { get; private set; } = ControllerState.Disconnected;
    public int? BatteryPercent { get; private set; }
    public bool CanVibrate => Info.Type.Model is ZwiftModel.Play or ZwiftModel.Ride;

    public event Action<ControllerState>? StateChanged;
    public event Action<IReadOnlyList<ButtonEvent>>? ButtonEvents;
    public event Action<int>? BatteryChanged;
    /// <summary>Trame brute (hexadécimale) pour l'onglet Diagnostic et la capture.</summary>
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
        // L'annulation sort de la boucle AVANT son ReleaseAllButtons : sans cet appel, une touche tenue pendant « Déconnecter »
        // resterait appuyée côté appli (action jamais relâchée).
        ReleaseAllButtons();
        SetState(ControllerState.Disconnected);
    }

    private async Task RunAsync(CancellationToken ct)
    {
        int delayMs = 1000;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                SetState(ControllerState.Connecting);
                bool ok = await ConnectOnceAsync(ct);
                if (ok)
                {
                    // Manette verrouillée (Click v2) : on ne rejoue pas la connexion toutes les 3 s indéfiniment, l'attente
                    // continue de croître (jusqu'à 15 s). Une vraie connexion remet l'attente à 1 s.
                    delayMs = State == ControllerState.Locked ? Math.Min(delayMs, 15000) : 1000;
                    // Attend la perte de connexion ou le verrouillage, avec un chien de garde : la manette émet au moins
                    // une trame par seconde. Six secondes de silence = notifications mortes => on se reconnecte.
                    while (!_disconnected!.Task.IsCompleted)
                    {
                        await Task.WhenAny(_disconnected.Task, Task.Delay(2000, ct));
                        ct.ThrowIfCancellationRequested();
                        if (State == ControllerState.Connected && Environment.TickCount64 - _lastFrameTick > 6000)
                        {
                            Log?.Invoke($"{Info.Name} : aucune notification depuis 6 s, reconnexion");
                            break;
                        }
                    }
                }
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
            if (State != ControllerState.Locked) SetState(ControllerState.Disconnected);

            try { await Task.Delay(delayMs, ct); } catch (OperationCanceledException) { break; }
            delayMs = Math.Min(delayMs * 2, 15000); // 1 s, 2 s, 4 s … 15 s
        }
        SetState(ControllerState.Disconnected);
    }

    private async Task<bool> ConnectOnceAsync(CancellationToken ct)
    {
        _disconnected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        try { _connection = await _central.ConnectAsync(Info.Address, new BleConnectOptions(), ct); }
        catch (BleDeviceNotFoundException) { throw new IOException(Strings.Err_ControllerNotFound); }
        _connection.Disconnected += OnDisconnected;
        _connection.ValueChanged += OnValueChanged;
        _connection.Log += m => Log?.Invoke(m);

        var services = await _connection.DiscoverAsync(
            uuid => uuid == ZwiftConstants.RideCustomService || uuid == ZwiftConstants.CustomService, strict: true, ct: ct);

        var service = services.FirstOrDefault(s => s.Uuid == ZwiftConstants.RideCustomService)
                      ?? services.FirstOrDefault(s => s.Uuid == ZwiftConstants.CustomService);
        if (service is null)
        {
            // Click v2 / Ride v2 non déverrouillés : le service Zwift n'est pas exposé.
            Log?.Invoke($"{Info.Name} : service Zwift absent, manette verrouillée");
            SetState(ControllerState.Locked);
            if (ReconnectOnLock) _ = Task.Delay(2000, ct).ContinueWith(_ => _disconnected.TrySetResult(), TaskScheduler.Default);
            else _disconnected.TrySetResult();
            return true;
        }

        var chars = service.Characteristics;
        bool hasAsync = chars.Any(c => c.Uuid == ZwiftConstants.AsyncCharacteristic);
        bool hasSyncTx = chars.Any(c => c.Uuid == ZwiftConstants.SyncTxCharacteristic);
        bool hasSyncRx = chars.Any(c => c.Uuid == ZwiftConstants.SyncRxCharacteristic);
        if (!hasAsync || !hasSyncTx || !hasSyncRx)
            throw new IOException(Strings.Err_ZwiftCharsMissing);

        _service = service.Uuid;
        _lastFrameTick = Environment.TickCount64;

        // Le gestionnaire ValueChanged est posé AVANT d'activer les notifications pour ne perdre aucune trame.
        await _connection.SetNotifyAsync(_service, ZwiftConstants.AsyncCharacteristic, BleNotifyMode.Notify);
        await _connection.SetNotifyAsync(_service, ZwiftConstants.SyncTxCharacteristic, BleNotifyMode.Indicate);

        await WriteAsync(ZwiftConstants.RideOn, withResponse: false);
        Log?.Invoke($"{Info.Name} : handshake RideOn envoyé");

        _ = ReadBatteryAsync(); // best-effort
        SetState(ControllerState.Connected);
        return true;
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
            if (b.Length > 0) SetBattery(b[0]);
        }
        catch { /* la batterie n'est pas critique */ }
    }

    private void OnValueChanged(Guid service, Guid characteristic, byte[] data)
    {
        _lastFrameTick = Environment.TickCount64;
        string hex = Convert.ToHexString(data);

        // Trames d'arrêt Click v2 : la manette se verrouille.
        if (data.Length >= 5 && data[0] == 0xFF && data[1] == 0x05 && data[2] == 0x00 && (data[3] is 0xEA or 0xFA) && data[4] == 0x05)
        {
            Frame?.Invoke($"{Info.Name} < {hex}");
            Log?.Invoke($"{Info.Name} : trame d'arrêt reçue, manette verrouillée");
            SetState(ControllerState.Locked);
            if (ReconnectOnLock) _disconnected?.TrySetResult();
            return;
        }

        var frame = ZwiftFrameParser.Parse(Info.Type, data);
        List<ButtonEvent>? events = null;
        if (frame?.Pressed is not null) lock (_trackerLock) events = _tracker.Update(frame.Pressed);

        // Les trames de boutons arrivent à ~30 Hz même au repos : on ne journalise que les changements
        // (ou tout, si le journal détaillé est activé). Les trames inconnues et la batterie sont toujours loguées.
        bool isButtonFrame = frame?.Pressed is not null;
        bool firstButtonFrame = isButtonFrame && _buttonFrameCount == 0;
        if (isButtonFrame) _buttonFrameCount++;
        if (VerboseFrames || !isButtonFrame || firstButtonFrame || events is { Count: > 0 })
            Frame?.Invoke($"{Info.Name} < {hex}");

        // Preuve de vie : un résumé toutes les 15 s, déclenché par les trames elles-mêmes (pas de timer).
        long now = Environment.TickCount64;
        if (now - _lastStatTick >= 15000)
        {
            if (_lastStatTick != 0)
                Log?.Invoke($"{Info.Name} : {_buttonFrameCount - _countAtLastStat} trame(s) de boutons reçue(s) ces 15 s");
            _lastStatTick = now;
            _countAtLastStat = _buttonFrameCount;
        }

        if (frame is null) return;
        if (frame.BatteryPercent is { } pct) SetBattery(pct);
        if (events is { Count: > 0 }) ButtonEvents?.Invoke(events);
    }

    private void OnDisconnected()
    {
        Log?.Invoke($"{Info.Name} : déconnectée");
        _disconnected?.TrySetResult();
    }

    /// <summary>Fait vibrer la manette (Play et Ride). Silencieux si non supporté.</summary>
    public async Task VibrateAsync(byte pattern = 0x20)
    {
        if (!CanVibrate || State != ControllerState.Connected) return;
        // Garde-fou : jamais plus d'une vibration toutes les 120 ms (évite de saturer la manette si une boucle s'emballe).
        long now = Environment.TickCount64;
        if (now - _lastVibrationTick < 120) return;
        _lastVibrationTick = now;
        try { await WriteAsync(ZwiftConstants.VibrationCommand(pattern), withResponse: false); }
        catch (Exception ex) { Log?.Invoke($"Vibration impossible : {ex.Message}"); }
    }

    private async Task WriteAsync(byte[] data, bool withResponse)
    {
        var connection = _connection;
        if (connection is null) return;
        await _writeLock.WaitAsync();
        try
        {
            await connection.WriteAsync(_service, ZwiftConstants.SyncRxCharacteristic, data, withResponse);
            Frame?.Invoke($"{Info.Name} > {Convert.ToHexString(data)}");
        }
        finally { _writeLock.Release(); }
    }

    private void ReleaseAllButtons()
    {
        List<ButtonEvent> events;
        lock (_trackerLock) events = _tracker.Reset(); // le thread BLE peut mettre à jour le suivi en même temps
        if (events.Count > 0) ButtonEvents?.Invoke(events); // évite une touche « bloquée » après une coupure
    }

    private void SetBattery(int pct)
    {
        BatteryPercent = pct;
        BatteryChanged?.Invoke(pct);
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
