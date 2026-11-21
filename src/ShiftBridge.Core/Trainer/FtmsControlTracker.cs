namespace ShiftBridge.Core.Trainer;

/// <summary>
/// Mémorise ce que l'APPLI a demandé au trainer (contrôle, démarrage, dernière cible), pour deux usages :
/// ne ré-appliquer un changement de rapport que si la dernière cible est une simulation (sinon on ferait sortir
/// le trainer d'un mode ERG), et rejouer l'état après une reconnexion (le contrôle FTMS est perdu à la coupure).
/// Les accès sont sérialisés : les événements Bluetooth arrivent sur n'importe quel thread.
/// </summary>
public sealed class FtmsControlTracker
{
    private readonly object _lock = new();
    private bool _controlRequested;
    private bool _started;
    private byte[]? _lastTarget;

    public void OnAppCommand(ReadOnlySpan<byte> cp)
    {
        if (cp.IsEmpty) return;
        byte op = cp[0];
        lock (_lock)
        {
            switch (op)
            {
                case FtmsCodec.OpRequestControl: _controlRequested = true; break;
                case FtmsCodec.OpStart: _started = true; break;
                case FtmsCodec.OpStop: _started = false; break;
                case FtmsCodec.OpReset: _started = false; _lastTarget = null; break; // un Reset n'est jamais rejoué
                default:
                    if (FtmsCodec.IsTargetCommand(op)) _lastTarget = cp.ToArray();
                    break;
            }
        }
    }

    /// <summary>Dernière commande cible envoyée par l'appli (quel que soit son type), ou null.</summary>
    public byte[]? LastTarget { get { lock (_lock) return _lastTarget; } }

    /// <summary>Vrai seulement si la dernière cible est une simulation : c'est la seule qu'un changement de rapport peut modifier.</summary>
    public bool CanReapplySimulation { get { lock (_lock) return _lastTarget is { Length: > 0 } t && t[0] == FtmsCodec.OpSetSimulation; } }

    /// <summary>
    /// Séquence à rejouer après une reconnexion : demande de contrôle, démarrage si l'appli avait démarré (et pas arrêté),
    /// puis la dernière cible. <paramref name="adjustSimulation"/> ajuste la pente d'une simulation au rapport courant.
    /// </summary>
    public IReadOnlyList<byte[]> ReplaySequence(Func<byte[], byte[]> adjustSimulation)
    {
        lock (_lock)
        {
            var list = new List<byte[]>();
            if (_controlRequested) list.Add([FtmsCodec.OpRequestControl]);
            if (_started) list.Add([FtmsCodec.OpStart]);
            if (_lastTarget is { } t)
                list.Add(t[0] == FtmsCodec.OpSetSimulation ? adjustSimulation(t) : t);
            return list;
        }
    }
}
