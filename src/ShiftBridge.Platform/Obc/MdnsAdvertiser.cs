using Makaretu.Dns;

namespace ShiftBridge.Platform.Obc;

/// <summary>Annonce un service TCP sur le réseau local via mDNS (_openbikecontrol._tcp, _wahoo-fitness-tnp._tcp).</summary>
public sealed class MdnsAdvertiser : IDisposable
{
    private ServiceDiscovery? _discovery;
    private ServiceProfile? _profile;

    public void Advertise(string instanceName, string serviceType, ushort port, IDictionary<string, string> txt)
    {
        Stop();
        _profile = new ServiceProfile(instanceName, serviceType, port);
        foreach (var (k, v) in txt) _profile.AddProperty(k, v);
        _discovery = new ServiceDiscovery();
        _discovery.Advertise(_profile);
        _discovery.Announce(_profile);
    }

    public void Stop()
    {
        if (_discovery is not null && _profile is not null)
        {
            try { _discovery.Unadvertise(_profile); } catch { /* arrêt best-effort */ }
        }
        _discovery?.Dispose();
        _discovery = null;
        _profile = null;
    }

    public void Dispose() => Stop();
}
