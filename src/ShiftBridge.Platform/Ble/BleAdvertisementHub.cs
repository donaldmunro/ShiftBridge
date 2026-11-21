namespace ShiftBridge.Platform.Ble;

/// <summary>
/// UN seul scanner BLE pour toute l'application, partagé entre plusieurs consommateurs (manettes Zwift, trainers). Le scan
/// actif consomme du temps radio et pénalise les liaisons déjà ouvertes : il ne doit donc tourner que tant qu'au moins un
/// consommateur est abonné.
/// </summary>
public sealed class BleAdvertisementHub : IDisposable
{
    private readonly object _lock = new();
    private readonly List<Action<BleAdvertisement>> _handlers = [];
    private readonly IBleScanner _scanner;
    private bool _running;

    public BleAdvertisementHub(IBleScanner scanner)
    {
        _scanner = scanner;
        _scanner.Received += OnReceived;
        _scanner.Stopped += reason =>
        {
            lock (_lock) _running = false;
            Log?.Invoke($"Scan BLE arrêté : {reason}");
        };
    }

    public event Action<string>? Log;

    public bool IsScanning => _scanner.IsRunning;

    /// <summary>S'abonne aux annonces ; libérer l'objet retourné pour se désabonner (le scan s'arrête avec le dernier abonné).</summary>
    public IDisposable Subscribe(Action<BleAdvertisement> handler)
    {
        lock (_lock)
        {
            _handlers.Add(handler);
            if (!_running)
            {
                _running = true;
                _scanner.Start();
                Log?.Invoke("Scan BLE démarré");
            }
        }
        return new Subscription(() => Unsubscribe(handler));
    }

    private void Unsubscribe(Action<BleAdvertisement> handler)
    {
        lock (_lock)
        {
            _handlers.Remove(handler);
            if (_handlers.Count > 0 || !_running) return;
            _running = false;
            try { _scanner.Stop(); } catch { /* déjà arrêté */ }
            Log?.Invoke("Scan BLE arrêté (plus aucun consommateur)");
        }
    }

    private void OnReceived(BleAdvertisement adv)
    {
        Action<BleAdvertisement>[] snapshot;
        lock (_lock) snapshot = [.. _handlers];
        foreach (var h in snapshot)
        {
            try { h(adv); }
            catch { /* un consommateur défaillant ne doit pas priver les autres des annonces */ }
        }
    }

    public void Dispose()
    {
        lock (_lock) { _handlers.Clear(); _running = false; }
        _scanner.Received -= OnReceived;
        _scanner.Dispose();
    }

    private sealed class Subscription(Action onDispose) : IDisposable
    {
        private int _disposed;
        public void Dispose() { if (Interlocked.Exchange(ref _disposed, 1) == 0) onDispose(); }
    }
}
