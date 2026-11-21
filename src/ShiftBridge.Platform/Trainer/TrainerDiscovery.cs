using System.Net;
using System.Net.Sockets;
using Makaretu.Dns;

namespace ShiftBridge.Platform.Trainer;

public sealed record DiscoveredTrainer(string Name, string Host, int Port, IReadOnlyDictionary<string, string> Txt)
{
    public override string ToString() => $"{Name} ({Host}:{Port})";
}

/// <summary>Recherche mDNS des trainers DirCon (Wahoo KICKR, Zwift Hub…) : service _wahoo-fitness-tnp._tcp.</summary>
public static class TrainerDiscovery
{
    public static async Task<List<DiscoveredTrainer>> BrowseAsync(TimeSpan duration, CancellationToken ct = default)
    {
        var found = new Dictionary<string, DiscoveredTrainer>();
        using var mdns = new MulticastService();
        using var sd = new ServiceDiscovery(mdns);

        sd.ServiceInstanceDiscovered += (_, e) =>
        {
            try
            {
                var records = e.Message.Answers.Concat(e.Message.AdditionalRecords).ToList();
                var srv = records.OfType<SRVRecord>().FirstOrDefault(r => r.Name == e.ServiceInstanceName);
                if (srv is null) return;

                var addr = records.OfType<AddressRecord>()
                    .Where(a => a.Name == srv.Target && a.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.Address).FirstOrDefault();
                if (addr is null) return;

                var txt = new Dictionary<string, string>();
                foreach (var t in records.OfType<TXTRecord>().Where(t => t.Name == e.ServiceInstanceName))
                    foreach (var s in t.Strings)
                    {
                        int i = s.IndexOf('=');
                        if (i > 0) txt[s[..i]] = s[(i + 1)..];
                    }

                string name = e.ServiceInstanceName.Labels.FirstOrDefault() ?? e.ServiceInstanceName.ToString();
                lock (found) found[name] = new DiscoveredTrainer(name, addr.ToString(), srv.Port, txt);
            }
            catch { /* annonce mal formée : ignorée */ }
        };

        mdns.Start();
        sd.QueryServiceInstances("_wahoo-fitness-tnp._tcp");
        try { await Task.Delay(duration, ct); } catch (OperationCanceledException) { }
        lock (found) return [.. found.Values];
    }
}
