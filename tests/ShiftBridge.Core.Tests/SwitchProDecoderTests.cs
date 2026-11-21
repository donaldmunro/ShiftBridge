using ShiftBridge.Core.Input;
using ShiftBridge.Platform.Input;

namespace ShiftBridge.Core.Tests;

public class SwitchProDecoderTests
{
    private const string Dev = "057E_2009";
    private static ButtonKey K(string name) => ButtonKey.Joystick(Dev, name);

    private static byte[] Hex(string s) => Convert.FromHexString(s);

    // Rapports 0x30 réels d'une Pro Controller en Bluetooth (24 premiers octets), relevés pendant les essais.
    [Theory]
    [InlineData("302B2000000000F87F00F87F0C71FE05FE36110800120005")]   // au repos
    public void Idle_report_presses_nothing(string hex) =>
        Assert.Empty(new SwitchProDecoder(Dev).Decode(Hex(hex)));

    [Theory]
    [InlineData("30522080000000F87F00F87F0C94FEE9FC2A11E7FF130135", "ZR")]
    [InlineData("30DE2004000000F87F00F87F0C95042900C8102000F8FFE3", "B")]
    [InlineData("30BC2040000000F87F00F87F0C67041F003310BB00D2FFEF", "R")]
    [InlineData("30672001000000F87F00F87F0C9B045A00C610F7FF270005", "Y")]
    [InlineData("30CF2002000000F87F00F87F0C8B047000E410FAFF1900FC", "X")]
    [InlineData("300E2008000000F87F00F87F0C34054600AD10D8FF1500F3", "A")]
    [InlineData("30D82000020000F87F00F87F0C8E04B8009010F3FF760016", "Plus")]
    [InlineData("30E92000004000F87F00F87F0C1504B600EC10ABFF0100FC", "L")]
    [InlineData("30332000008000F87F00F87F0C62043F00AD106AFFECFF27", "ZL")]
    [InlineData("30E22000040000F87F0118840C7704E800CA1015004B0004", "RStick")]
    public void Real_button_reports_map_to_the_right_button(string hex, string expected)
    {
        var pressed = new SwitchProDecoder(Dev).Decode(Hex(hex));
        Assert.Equal(K(expected), Assert.Single(pressed));
    }

    [Fact]
    public void Two_buttons_at_once()
    {
        var pressed = new SwitchProDecoder(Dev).Decode(Hex("30712080008000F87F00F87F0C8EFD01FCA31013010B01DE"));
        Assert.Equal(2, pressed.Count);
        Assert.Contains(K("ZR"), pressed);
        Assert.Contains(K("ZL"), pressed);
    }

    [Fact]
    public void Dpad_bits_are_decoded()
    {
        var d = new SwitchProDecoder(Dev);
        byte[] Report(byte b5) { var r = new byte[48]; r[0] = 0x30; r[5] = b5; r[7] = 0xF8; r[8] = 0x7F; r[10] = 0xF8; r[11] = 0x7F; return r; }
        Assert.Contains(K("DPadDown"), d.Decode(Report(0x01)));
        Assert.Contains(K("DPadUp"), d.Decode(Report(0x02)));
        Assert.Contains(K("DPadRight"), d.Decode(Report(0x04)));
        Assert.Contains(K("DPadLeft"), d.Decode(Report(0x08)));
    }

    private static byte[] WithLeftStick(int x, int y)
    {
        var r = new byte[48]; r[0] = 0x30;
        r[6] = (byte)(x & 0xFF); r[7] = (byte)(((y & 0xF) << 4) | (x >> 8)); r[8] = (byte)(y >> 4);
        r[10] = 0xF8; r[11] = 0x7F; // stick droit au centre
        return r;
    }

    [Fact]
    public void Stick_directions_use_hysteresis_and_ignore_the_resting_centre()
    {
        var d = new SwitchProDecoder(Dev);
        Assert.Empty(d.Decode(WithLeftStick(2048, 2047)));                                   // centre
        Assert.Empty(d.Decode(WithLeftStick(2048 + 800, 2048)));                              // sous le seuil d'appui
        Assert.Contains(K("LStickRight"), d.Decode(WithLeftStick(2048 + 1200, 2048)));        // franchi
        Assert.Contains(K("LStickRight"), d.Decode(WithLeftStick(2048 + 800, 2048)));         // entre les deux seuils : tenu
        Assert.Empty(d.Decode(WithLeftStick(2048 + 500, 2048)));                              // relâché
        Assert.Contains(K("LStickUp"), d.Decode(WithLeftStick(2048, 2048 + 1200)));
        Assert.Contains(K("LStickLeft"), d.Decode(WithLeftStick(2048 - 1200, 2048)));
    }

    [Fact]
    public void Only_full_reports_are_decoded_others_fall_back_to_generic_hid()
    {
        Assert.True(SwitchProDecoder.CanDecode(new byte[48] .Select((_, i) => i == 0 ? (byte)0x30 : (byte)0).ToArray()));
        Assert.False(SwitchProDecoder.CanDecode(new byte[] { 0x3F, 0, 0, 0x08, 0, 0, 0, 0, 0, 0, 0, 0 })); // rapport simple 0x3F
        Assert.False(SwitchProDecoder.CanDecode(new byte[] { 0x30, 1, 2 }));                               // trop court
    }

    [Theory]
    [InlineData("057E", "2009", true)]
    [InlineData("057e", "2006", true)]
    [InlineData("057E", "9999", false)]
    [InlineData("046D", "2009", false)]
    public void Supported_nintendo_devices(string vid, string pid, bool expected) =>
        Assert.Equal(expected, SwitchProDecoder.Supports(vid, pid));
}

public class HidIdsTests
{
    [Theory]
    [InlineData(@"\\?\HID#{00001124-0000-1000-8000-00805f9b34fb}_VID&0002057e_PID&2009#9&1c5f3e9d&3&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", "057E", "2009")]
    [InlineData(@"\\?\HID#VID_046D&PID_C21D&MI_00#7&abc&0&0000#{4d1e55b2-f16f-11cf-88cb-001111000030}", "046D", "C21D")]
    [InlineData(@"\\?\HID#VID_1915&PID_1025&MI_00&COL02#8&4cdbfdf&0&0001#{x}", "1915", "1025")]
    [InlineData(@"\\?\ROOT#SYSTEM#0001", "0000", "0000")]
    public void Parses_usb_and_bluetooth_device_paths(string path, string vid, string pid) =>
        Assert.Equal((vid, pid), HidIds.Parse(path));

    [Fact]
    public void Nintendo_pads_get_a_friendly_label()
    {
        Assert.Equal("Pro Controller : ZR", ButtonKeyNames.Label(ButtonKey.Joystick("057E_2009", "ZR")));
        Assert.Equal("Pro Controller : +", ButtonKeyNames.Label(ButtonKey.Joystick("057E_2009", "Plus")));
        Assert.Equal("Pro Controller : Croix : haut", ButtonKeyNames.Label(ButtonKey.Joystick("057E_2009", "DPadUp")));
        Assert.Equal("Pro Controller : Stick gauche : droite", ButtonKeyNames.Label(ButtonKey.Joystick("057E_2009", "LStickRight")));
    }
}
