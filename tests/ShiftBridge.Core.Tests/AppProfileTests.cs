using ShiftBridge.Core.Config;
using ShiftBridge.Core.Input;
using ShiftBridge.Core.Mapping;
using ShiftBridge.Core.Obc;
using ShiftBridge.Platform;

namespace ShiftBridge.Core.Tests;

public class AppProfileTests
{
    private static async Task Until(Func<bool> cond, int ms = 3000)
    {
        var end = Environment.TickCount64 + ms;
        while (!cond()) { if (Environment.TickCount64 > end) throw new TimeoutException(); await Task.Delay(10); }
    }

    private static void Tap(BridgeService bridge, PhysicalButton button)
    {
        bridge.InjectEvents([new ButtonEvent(button, true)]);
        bridge.InjectEvents([new ButtonEvent(button, false)]);
    }

    private static byte[] Press(ObcButtonId id) => [0x01, (byte)id, 1];

    [Theory]
    [InlineData("TrainingPeaks Virtual", "trainingpeaks virtual")]
    [InlineData("  Zwift ", "zwift")]
    [InlineData(null, "")]
    [InlineData("   ", "")]
    public void App_keys_ignore_case_and_surrounding_spaces(string? appId, string expected) =>
        Assert.Equal(expected, AppProfileKey.Normalize(appId));

    [Fact]
    public async Task The_apps_profile_is_used_while_it_is_connected_and_the_default_one_otherwise()
    {
        await using var bridge = new BridgeService(new AppConfig());
        var app = new FakeTransport("appli");
        await bridge.AddTransportAsync(app);

        // Profil par défaut : « Retour » (RightB). Profil de l'appli : « Emote ».
        string key = bridge.CreateAppProfile("TrainingPeaks Virtual")!;
        bridge.GetProfile(key).Bindings[PhysicalButton.RightB] = new ButtonBinding { Actions = [ObcButtonId.Emote] };

        app.RaiseAppInfo(new ObcAppInfo(" TRAININGPEAKS virtual ", "1", []));
        Assert.Equal(key, bridge.ActiveProfileKey);
        Assert.Equal("TRAININGPEAKS virtual", bridge.ConnectedAppId);
        Tap(bridge, PhysicalButton.RightB);
        await Until(() => app.Sent.Count == 2);
        Assert.Equal(Press(ObcButtonId.Emote), app.Sent.First());

        app.Disconnect();
        Assert.Equal("", bridge.ActiveProfileKey);
        Assert.Same(bridge.DefaultProfile, bridge.Profile);
        Tap(bridge, PhysicalButton.RightB);
        await Until(() => app.Sent.Count == 4);
        Assert.Equal(Press(ObcButtonId.Back), app.Sent.ToArray()[2]);
    }

    [Fact]
    public async Task An_app_without_a_profile_uses_the_default_profile()
    {
        await using var bridge = new BridgeService(new AppConfig());
        var app = new FakeTransport("appli");
        await bridge.AddTransportAsync(app);
        bridge.CreateAppProfile("Zwift");

        app.RaiseAppInfo(new ObcAppInfo("MyWhoosh", "1", []));
        Assert.Equal("", bridge.ActiveProfileKey);
        Assert.Same(bridge.DefaultProfile, bridge.Profile);
        Assert.Equal("MyWhoosh", bridge.ConnectedAppId);
    }

    [Fact]
    public async Task Creating_a_profile_for_the_connected_app_activates_it_at_once_and_copies_the_default()
    {
        await using var bridge = new BridgeService(new AppConfig());
        var app = new FakeTransport("appli");
        await bridge.AddTransportAsync(app);
        app.RaiseAppInfo(new ObcAppInfo("Zwift", "1", []));
        int changes = 0;
        bridge.ActiveProfileChanged += () => changes++;

        string key = bridge.CreateAppProfile("Zwift")!;

        Assert.Equal("zwift", key);
        Assert.Equal(key, bridge.ActiveProfileKey);
        Assert.NotSame(bridge.DefaultProfile, bridge.Profile);
        Assert.Equal(bridge.DefaultProfile.Bindings.Count, bridge.Profile.Bindings.Count); // copie du profil par défaut
        Assert.Equal("Zwift", bridge.Profile.Name);
        Assert.True(changes >= 1);

        // Modifier le profil de l'appli ne touche pas au profil par défaut.
        bridge.Profile.Bindings.Remove(PhysicalButton.RightB);
        Assert.True(bridge.DefaultProfile.Bindings.ContainsKey(PhysicalButton.RightB));
    }

    [Fact]
    public async Task Deleting_the_active_profile_falls_back_to_the_default_one()
    {
        await using var bridge = new BridgeService(new AppConfig());
        var app = new FakeTransport("appli");
        await bridge.AddTransportAsync(app);
        string key = bridge.CreateAppProfile("Zwift")!;
        app.RaiseAppInfo(new ObcAppInfo("Zwift", "1", []));
        Assert.Equal(key, bridge.ActiveProfileKey);

        bridge.RemoveAppProfile(key);

        Assert.Equal("", bridge.ActiveProfileKey);
        Assert.Same(bridge.DefaultProfile, bridge.Profile);
        Assert.Empty(bridge.AppProfiles);
    }

    [Fact]
    public async Task Resetting_a_profile_restores_the_original_keys_and_keeps_its_name()
    {
        await using var bridge = new BridgeService(new AppConfig());
        string key = bridge.CreateAppProfile("Zwift")!;
        bridge.GetProfile(key).Bindings.Clear();

        bridge.ResetProfile(key);

        var profile = bridge.GetProfile(key);
        Assert.Equal("Zwift", profile.Name);
        Assert.Equal(Profile.ZwiftLike().Bindings.Count, profile.Bindings.Count);
    }

    [Fact]
    public async Task Resetting_the_default_profile_replaces_it_and_keeps_it_active()
    {
        await using var bridge = new BridgeService(new AppConfig());
        bridge.Profile.Bindings.Clear();

        bridge.ResetProfile("");

        Assert.Same(bridge.DefaultProfile, bridge.Profile);
        Assert.Same(bridge.Config.Profile, bridge.DefaultProfile);
        Assert.Equal(Profile.ZwiftLike().Bindings.Count, bridge.Profile.Bindings.Count);
    }

    [Fact]
    public void App_profiles_are_saved_and_an_old_config_without_them_still_loads()
    {
        string path = Path.Combine(Path.GetTempPath(), $"shiftbridge-test-{Guid.NewGuid():N}.json");
        try
        {
            var config = new AppConfig();
            var profile = Profile.ZwiftLike();
            profile.Name = "Zwift";
            profile.Bindings[PhysicalButton.RightA] = new ButtonBinding { Actions = [ObcButtonId.Emote] };
            config.AppProfiles["zwift"] = profile;
            ConfigStore.Save(config, path);

            var back = ConfigStore.Load(path);
            Assert.Equal([ObcButtonId.Emote], back.AppProfiles["zwift"].Bindings[PhysicalButton.RightA].Actions);
            Assert.Equal("Zwift", back.AppProfiles["zwift"].Name);

            File.WriteAllText(path, """{ "GearCount": 20 }"""); // config d'une version précédente
            var old = ConfigStore.Load(path);
            Assert.NotNull(old.AppProfiles);
            Assert.Empty(old.AppProfiles);
            Assert.Equal(20, old.GearCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Hand_edited_profile_keys_are_normalised_and_empty_ones_dropped()
    {
        string path = Path.Combine(Path.GetTempPath(), $"shiftbridge-test-{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(path, """
                { "AppProfiles": { " Zwift ": { "Name": "Zwift", "Bindings": null }, "  ": { "Name": "vide" }, "Nul": null } }
                """);
            var c = ConfigStore.Load(path);
            Assert.Equal(["zwift"], c.AppProfiles.Keys.ToArray());
            Assert.NotNull(c.AppProfiles["zwift"].Bindings);
        }
        finally { File.Delete(path); }
    }
}

public class AppProfileOverDirConTests
{
    [Fact]
    public async Task A_real_dircon_session_makes_the_app_known_to_the_bridge()
    {
        await using var bridge = new BridgeService(new AppConfig());
        var dircon = new ShiftBridge.Platform.Obc.DirConServer();
        await bridge.AddTransportAsync(dircon);
        var changes = new List<string>();
        bridge.ActiveProfileChanged += () => changes.Add(bridge.ConnectedAppId);

        using var client = new System.Net.Sockets.TcpClient();
        await client.ConnectAsync("127.0.0.1", dircon.Port);
        var s = client.GetStream();
        // Séquence réelle de TPV : activation des notifications, puis App Info.
        await s.WriteAsync(new ShiftBridge.Core.DirCon.DirConPacket(0x05, 1, 0, [.. ShiftBridge.Core.DirCon.DirConPacket.UuidBytes(ShiftBridge.Platform.Obc.ObcGattUuids.ButtonState)]).Encode());
        var info = ObcMessages.EncodeAppInfo(new ObcAppInfo("trainingpeaks", "3.0", []));
        await s.WriteAsync(new ShiftBridge.Core.DirCon.DirConPacket(0x04, 2, 0, [.. ShiftBridge.Core.DirCon.DirConPacket.UuidBytes(ShiftBridge.Platform.Obc.ObcGattUuids.AppInfo), .. info]).Encode());

        var end = Environment.TickCount64 + 3000;
        while (bridge.ConnectedAppId.Length == 0 && Environment.TickCount64 < end) await Task.Delay(10);
        await Task.Delay(300); // laisse passer d'éventuelles notifications de clients tardives

        Assert.Equal("trainingpeaks", bridge.ConnectedAppId);
        Assert.Equal("trainingpeaks", bridge.ConnectedAppKey);
    }
}
