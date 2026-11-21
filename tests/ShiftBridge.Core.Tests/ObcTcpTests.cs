using System.Net.Sockets;
using ShiftBridge.Core.Obc;
using ShiftBridge.Platform.Obc;

namespace ShiftBridge.Core.Tests;

public class ObcStreamParserTests
{
    [Fact]
    public void Reassembles_split_app_info_and_haptic()
    {
        var info = ObcMessages.EncodeAppInfo(new ObcAppInfo("tpv", "1.0", [0x01, 0x02]));
        var haptic = ObcMessages.EncodeHaptic(new ObcHaptic(ObcHapticPattern.Short, 0, 0));
        var all = info.Concat(haptic).ToArray();

        var parser = new ObcStreamParser();
        var msgs = new List<byte[]>();
        foreach (var b in all) msgs.AddRange(parser.Feed([b])); // octet par octet : pire cas

        Assert.Equal(2, msgs.Count);
        Assert.Equal(info, msgs[0]);
        Assert.Equal(haptic, msgs[1]);
    }

    [Fact]
    public void Resynchronizes_after_unknown_bytes()
    {
        var haptic = ObcMessages.EncodeHaptic(new ObcHaptic(ObcHapticPattern.Double, 10, 0x80));
        var msgs = new ObcStreamParser().Feed([0xAA, 0xBB, .. haptic]).ToList();
        Assert.Equal([haptic], msgs);
    }
}

public class ObcTcpServerTests
{
    [Fact]
    public async Task Client_receives_status_and_button_state_and_server_receives_app_info_and_haptic()
    {
        await using var server = new ObcTcpServer("ShiftBridgeTest");
        var appInfo = new TaskCompletionSource<ObcAppInfo>();
        var haptic = new TaskCompletionSource<ObcHaptic>();
        server.AppInfoReceived += appInfo.SetResult;
        server.HapticReceived += haptic.SetResult;
        await server.StartAsync();

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", server.Port);
        var stream = client.GetStream();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        // 1) statut envoyé à la connexion
        var buf = new byte[3];
        await stream.ReadExactlyAsync(buf, timeout.Token);
        Assert.Equal(new byte[] { 0x02, 0xFF, 0x01 }, buf);

        // 2) App Info (envoyé en deux morceaux) puis haptique
        var info = ObcMessages.EncodeAppInfo(new ObcAppInfo("trainingpeaks", "2.0", [0x01, 0x02]));
        await stream.WriteAsync(info.AsMemory(0, 5), timeout.Token);
        await stream.FlushAsync(timeout.Token);
        await Task.Delay(50, timeout.Token);
        await stream.WriteAsync(info.AsMemory(5), timeout.Token);
        await stream.WriteAsync(ObcMessages.EncodeHaptic(new ObcHaptic(ObcHapticPattern.Success, 0, 0)), timeout.Token);

        Assert.Equal("trainingpeaks", (await appInfo.Task.WaitAsync(timeout.Token)).AppId);
        Assert.Equal(ObcHapticPattern.Success, (await haptic.Task.WaitAsync(timeout.Token)).Pattern);
        Assert.Equal("trainingpeaks", server.CurrentAppInfo!.AppId);

        // 3) état de bouton serveur -> client
        await server.SendButtonStateAsync([0x01, 0x01, 0x01]);
        var btn = new byte[3];
        await stream.ReadExactlyAsync(btn, timeout.Token);
        Assert.Equal(new byte[] { 0x01, 0x01, 0x01 }, btn);
    }

    [Fact]
    public async Task Disconnect_clears_client_and_app_info()
    {
        await using var server = new ObcTcpServer("ShiftBridgeTest2");
        await server.StartAsync();
        var gone = new TaskCompletionSource();
        server.ClientsChanged += () => { if (!server.HasClient) gone.TrySetResult(); };

        using (var client = new TcpClient())
        {
            await client.ConnectAsync("127.0.0.1", server.Port);
            await Task.Delay(100);
            Assert.True(server.HasClient);
        }
        await gone.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(server.HasClient);
        Assert.Null(server.CurrentAppInfo);
    }
}
