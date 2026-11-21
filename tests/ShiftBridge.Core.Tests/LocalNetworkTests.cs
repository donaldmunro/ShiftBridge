using ShiftBridge.Presentation;

namespace ShiftBridge.Core.Tests;

public class LocalNetworkTests
{
    [Fact]
    public void Describe_lists_every_address_then_loopback_and_the_active_ports()
    {
        string text = LocalNetwork.Describe(["192.168.1.20", "10.0.0.5"], ("OBC réseau", 36867), ("DirCon", 36866));
        Assert.Equal(
            "Adresses : 192.168.1.20 (depuis un autre appareil) · 10.0.0.5 (depuis un autre appareil) · 127.0.0.1 (cet appareil). " +
            "Ports : OBC réseau 36867, DirCon 36866.", text);
    }

    [Fact]
    public void Describe_skips_disabled_listeners()
    {
        string text = LocalNetwork.Describe(["192.168.1.20"], ("OBC réseau", 36867), ("DirCon", 0));
        Assert.Contains("OBC réseau 36867", text);
        Assert.DoesNotContain("DirCon", text);
    }

    [Fact]
    public void Describe_is_empty_without_any_listener()
    {
        Assert.Equal("", LocalNetwork.Describe(["192.168.1.20"], ("OBC réseau", 0), ("DirCon", 0)));
        Assert.Equal("", LocalNetwork.Describe([]));
    }

    [Fact]
    public void Describe_still_offers_loopback_when_offline()
    {
        string text = LocalNetwork.Describe([], ("OBC réseau", 36867));
        Assert.Contains("127.0.0.1 (cet appareil)", text);
    }

    [Fact]
    public void Real_addresses_never_include_loopback_or_link_local()
    {
        var addresses = LocalNetwork.GetIPv4Addresses();
        Assert.DoesNotContain("127.0.0.1", addresses);
        Assert.DoesNotContain(addresses, a => a.StartsWith("169.254."));
        Assert.Equal(addresses.Distinct().Count(), addresses.Count);
    }
}
