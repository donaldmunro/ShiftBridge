using System.Net.Sockets;

namespace ShiftBridge.Platform.Obc;

/// <summary>
/// Écriture TCP bornée dans le temps. Un client dont le tampon de réception est plein (appli figée, Wi-Fi coupé sans
/// fermeture) bloquerait sinon l'écriture indéfiniment : les écritures d'un transport sont sérialisées, et la boucle d'envoi des
/// boutons est unique, donc plus aucune touche ne partirait vers aucune appli.
/// </summary>
internal static class TcpSend
{
    public static TimeSpan WriteTimeout { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>Écrit <paramref name="data"/> ; au-delà du délai, appelle <paramref name="drop"/> (le message est peut-être à moitié parti : le flux est inutilisable).</summary>
    public static async Task WriteAsync(NetworkStream stream, byte[] data, Action drop)
    {
        using var cts = new CancellationTokenSource(WriteTimeout);
        try { await stream.WriteAsync(data, cts.Token); }
        // L'annulation d'une écriture en cours remonte selon la plateforme en OperationCanceledException, IOException ou
        // SocketException : dans tous les cas où NOTRE délai a expiré, le flux est inutilisable.
        catch (Exception ex) when (cts.IsCancellationRequested && ex is OperationCanceledException or IOException or SocketException)
        {
            drop();
        }
    }
}
