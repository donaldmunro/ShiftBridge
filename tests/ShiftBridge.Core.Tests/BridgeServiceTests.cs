using System.Net.Sockets;
using ShiftBridge.Core.Config;
using ShiftBridge.Core.Input;
using ShiftBridge.Core.Obc;
using ShiftBridge.Platform;
using ShiftBridge.Platform.Obc;

namespace ShiftBridge.Core.Tests;

public class BridgeServiceTests
{
    private static async Task<byte[]> ReadAsync(NetworkStream s, int n, CancellationToken ct)
    {
        var buf = new byte[n];
        await s.ReadExactlyAsync(buf, ct);
        return buf;
    }

    [Fact]
    public async Task Click_press_reaches_tcp_trainer_app_in_order_and_updates_gear()
    {
        await using var bridge = new BridgeService(new AppConfig { GearCount = 24 });
        var tcp = new ObcTcpServer("BridgeTest");
        await bridge.AddTransportAsync(tcp);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", tcp.Port);
        var s = client.GetStream();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await ReadAsync(s, 3, cts.Token); // statut initial

        int startGear = bridge.Gear.Gear;
        bridge.InjectEvents([new ButtonEvent(PhysicalButton.ClickPlus, true)]);
        bridge.InjectEvents([new ButtonEvent(PhysicalButton.ClickPlus, false)]);
        bridge.InjectEvents([new ButtonEvent(PhysicalButton.ClickMinus, true)]);
        bridge.InjectEvents([new ButtonEvent(PhysicalButton.ClickMinus, false)]);

        Assert.Equal(new byte[] { 0x01, 0x01, 0x01 }, await ReadAsync(s, 3, cts.Token)); // ShiftUp appuyé
        Assert.Equal(new byte[] { 0x01, 0x01, 0x00 }, await ReadAsync(s, 3, cts.Token)); // ShiftUp relâché
        Assert.Equal(new byte[] { 0x01, 0x02, 0x01 }, await ReadAsync(s, 3, cts.Token)); // ShiftDown appuyé
        Assert.Equal(new byte[] { 0x01, 0x02, 0x00 }, await ReadAsync(s, 3, cts.Token));
        Assert.Equal(startGear, bridge.Gear.Gear); // +1 puis -1
    }

    [Fact]
    public async Task App_info_restricts_actions_sent_to_the_app()
    {
        await using var bridge = new BridgeService(new AppConfig());
        var tcp = new ObcTcpServer("BridgeTest2");
        var got = new TaskCompletionSource();
        tcp.AppInfoReceived += _ => got.TrySetResult();
        await bridge.AddTransportAsync(tcp);

        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", tcp.Port);
        var s = client.GetStream();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await ReadAsync(s, 3, cts.Token);

        // L'appli ne déclare que ShiftUp (0x01)
        await s.WriteAsync(ObcMessages.EncodeAppInfo(new ObcAppInfo("test", "1", [0x01])), cts.Token);
        await got.Task.WaitAsync(cts.Token);
        await Task.Delay(50, cts.Token); // laisse le BridgeService appliquer le filtre

        bridge.InjectEvents([new ButtonEvent(PhysicalButton.RightB, true)]);   // Back : filtré
        bridge.InjectEvents([new ButtonEvent(PhysicalButton.RightB, false)]);
        bridge.InjectEvents([new ButtonEvent(PhysicalButton.ClickPlus, true)]); // ShiftUp : passe

        Assert.Equal(new byte[] { 0x01, 0x01, 0x01 }, await ReadAsync(s, 3, cts.Token));
    }
}

public class BridgeServiceRobustnessTests
{
    [Fact]
    public async Task Set_auto_reconnect_updates_config_immediately()
    {
        var config = new AppConfig { AutoReconnect = true };
        await using var bridge = new BridgeService(config);
        bridge.SetAutoReconnect(false);
        Assert.False(config.AutoReconnect);
        bridge.SetAutoReconnect(true);
        Assert.True(config.AutoReconnect);
    }

    [Fact]
    public async Task Concurrent_injections_keep_press_before_release_per_button()
    {
        await using var bridge = new BridgeService(new AppConfig());
        var tcp = new ObcTcpServer("BridgeTest3");
        await bridge.AddTransportAsync(tcp);
        using var client = new TcpClient();
        await client.ConnectAsync("127.0.0.1", tcp.Port);
        var s = client.GetStream();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var status = new byte[3];
        await s.ReadExactlyAsync(status, cts.Token); // statut initial

        const int rounds = 100;
        var t1 = Task.Run(() => { for (int i = 0; i < rounds; i++) { bridge.InjectEvents([new ButtonEvent(PhysicalButton.RightB, true)]); bridge.InjectEvents([new ButtonEvent(PhysicalButton.RightB, false)]); } });
        var t2 = Task.Run(() => { for (int i = 0; i < rounds; i++) { bridge.InjectEvents([new ButtonEvent(PhysicalButton.RightA, true)]); bridge.InjectEvents([new ButtonEvent(PhysicalButton.RightA, false)]); } });
        await Task.WhenAll(t1, t2);

        // Chaque message fait 3 octets ; pour une action donnée, on ne doit jamais lire « relâché » avant « appuyé ».
        var pressed = new Dictionary<byte, bool>();
        for (int i = 0; i < rounds * 4; i++)
        {
            var m = new byte[3];
            await s.ReadExactlyAsync(m, cts.Token);
            bool down = m[2] == 1;
            Assert.NotEqual(pressed.GetValueOrDefault(m[1]), down); // alternance stricte appui/relâchement
            pressed[m[1]] = down;
        }
    }
}
