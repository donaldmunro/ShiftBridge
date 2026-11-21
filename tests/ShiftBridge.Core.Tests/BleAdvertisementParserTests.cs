using System.Text;
using ShiftBridge.Core.Trainer;
using ShiftBridge.Core.Zwift;
using ShiftBridge.Platform.Ble;

namespace ShiftBridge.Core.Tests;

public class BleAdvertisementParserTests
{
    /// <summary>Une structure AD : [longueur, type, données].</summary>
    private static byte[] Ad(byte type, params byte[] data) => [(byte)(data.Length + 1), type, .. data];

    private static byte[] Raw(params byte[][] structures) => [.. structures.SelectMany(s => s)];

    // 6e40fec1-b5a3-f393-e0a9-e50e24dcca9e transmis octet de poids faible en premier
    private static readonly byte[] FecLittleEndian =
        [0x9e, 0xca, 0xdc, 0x24, 0x0e, 0xe5, 0xa9, 0xe0, 0x93, 0xf3, 0xa3, 0xb5, 0xc1, 0xfe, 0x40, 0x6e];

    [Fact]
    public void Parses_a_typical_trainer_advertisement()
    {
        byte[] raw = Raw(
            Ad(0x01, 0x06),                                  // flags : ignorés
            Ad(0x09, Encoding.UTF8.GetBytes("KICKR CORE 1234")),
            Ad(0x03, 0x26, 0x18, 0x18, 0x18),                // FTMS (0x1826) + puissance (0x1818)
            Ad(0x16, 0x26, 0x18, 0x01, 0x20, 0x00));         // données de service FTMS : flags + type vélo

        var r = BleAdvertisementParser.Parse(raw);

        Assert.Equal("KICKR CORE 1234", r.LocalName);
        Assert.Equal([FtmsCodec.FitnessMachineService, FtmsCodec.BaseUuid(0x1818)], r.ServiceUuids);
        Assert.Equal(new byte[] { 0x01, 0x20, 0x00 }, r.ServiceData[FtmsCodec.FitnessMachineService]);
        Assert.True(FtmsAdvertisement.TryParse(r.ServiceData[FtmsCodec.FitnessMachineService], out var ftms));
        Assert.True(ftms.IsIndoorBike);
    }

    [Fact]
    public void Parses_zwift_manufacturer_data_with_company_id_first()
    {
        // company id 0x094A en petit-boutiste, puis le code appareil (0x09 = Click)
        var r = BleAdvertisementParser.Parse(Raw(Ad(0xFF, 0x4A, 0x09, 0x09, 0x01)));

        var (company, data) = Assert.Single(r.ManufacturerData);
        Assert.Equal(ZwiftConstants.ManufacturerId, company);
        Assert.Equal(new byte[] { 0x09, 0x01 }, data);
        Assert.Equal(ZwiftModel.Click, ZwiftDeviceType.FromManufacturerData(data).Model);
    }

    [Fact]
    public void Keeps_several_manufacturer_blocks()
    {
        var r = BleAdvertisementParser.Parse(Raw(Ad(0xFF, 0x4C, 0x00, 0x01), Ad(0xFF, 0x4A, 0x09, 0x03)));
        Assert.Equal([(ushort)0x004C, (ushort)0x094A], r.ManufacturerData.Select(m => m.CompanyId));
    }

    [Fact]
    public void Decodes_128_bit_uuids_into_display_order()
    {
        var r = BleAdvertisementParser.Parse(Raw(Ad(0x07, FecLittleEndian)));
        Assert.Equal([TrainerAdvertisementRules.FecOverBleService], r.ServiceUuids);
    }

    [Fact]
    public void Decodes_the_obc_128_bit_uuid()
    {
        var obc = new Guid("d273f680-d548-419d-b9d1-fa0472345229");
        byte[] le = [0x29, 0x52, 0x34, 0x72, 0x04, 0xfa, 0xd1, 0xb9, 0x9d, 0x41, 0x48, 0xd5, 0x80, 0xf6, 0x73, 0xd2];
        var r = BleAdvertisementParser.Parse(Raw(Ad(0x06, le)));
        Assert.Equal(obc, Assert.Single(r.ServiceUuids));
    }

    [Fact]
    public void Decodes_32_bit_uuids_and_service_data()
    {
        var r = BleAdvertisementParser.Parse(Raw(
            Ad(0x05, 0x26, 0x18, 0x00, 0x00),
            Ad(0x20, 0x0F, 0x18, 0x00, 0x00, 0x55)));
        Assert.Equal(FtmsCodec.FitnessMachineService, Assert.Single(r.ServiceUuids));
        Assert.Equal(new byte[] { 0x55 }, r.ServiceData[FtmsCodec.BaseUuid(0x180F)]);
    }

    [Fact]
    public void Decodes_128_bit_service_data()
    {
        var r = BleAdvertisementParser.Parse(Raw(Ad(0x21, [.. FecLittleEndian, 0xAA, 0xBB])));
        Assert.Equal(new byte[] { 0xAA, 0xBB }, r.ServiceData[TrainerAdvertisementRules.FecOverBleService]);
    }

    [Fact]
    public void Complete_name_wins_over_shortened_name_in_any_order()
    {
        byte[] a = Raw(Ad(0x08, (byte)'K'), Ad(0x09, "Kickr"u8.ToArray()));
        byte[] b = Raw(Ad(0x09, "Kickr"u8.ToArray()), Ad(0x08, (byte)'K'));
        Assert.Equal("Kickr", BleAdvertisementParser.Parse(a).LocalName);
        Assert.Equal("Kickr", BleAdvertisementParser.Parse(b).LocalName);
        Assert.Equal("K", BleAdvertisementParser.Parse(Raw(Ad(0x08, (byte)'K'))).LocalName);
    }

    [Fact]
    public void Name_is_utf8_and_trims_trailing_nul()
    {
        Assert.Equal("Élite", BleAdvertisementParser.Parse(Raw(Ad(0x09, Encoding.UTF8.GetBytes("Élite")))).LocalName);
        Assert.Equal("Hub", BleAdvertisementParser.Parse(Raw(Ad(0x09, (byte)'H', (byte)'u', (byte)'b', 0x00))).LocalName);
        Assert.Null(BleAdvertisementParser.Parse(Raw(Ad(0x09, 0x00))).LocalName);
    }

    [Fact]
    public void Empty_or_garbage_input_never_throws()
    {
        Assert.Null(BleAdvertisementParser.Parse([]).LocalName);
        Assert.Empty(BleAdvertisementParser.Parse([]).ServiceUuids);

        for (int seed = 0; seed < 200; seed++)
        {
            var rnd = new Random(seed);
            byte[] noise = new byte[rnd.Next(1, 64)];
            rnd.NextBytes(noise);
            BleAdvertisementParser.Parse(noise); // ne doit pas lever
        }
    }

    [Fact]
    public void Stops_at_a_truncated_structure_but_keeps_what_was_read()
    {
        byte[] raw = [.. Ad(0x09, "OK"u8.ToArray()), 0x09, 0x16, 0x26]; // dernière structure annonce 9 octets, n'en a que 2
        var r = BleAdvertisementParser.Parse(raw);
        Assert.Equal("OK", r.LocalName);
        Assert.Empty(r.ServiceData);
    }

    [Fact]
    public void Zero_length_marks_the_end_of_significant_data()
    {
        byte[] raw = [.. Ad(0x09, "A"u8.ToArray()), 0x00, .. Ad(0x08, (byte)'Z')];
        Assert.Equal("A", BleAdvertisementParser.Parse(raw).LocalName);
    }

    [Fact]
    public void Ignores_too_short_payloads_instead_of_crashing()
    {
        // manufacturer < 2 octets, service data 16 bits < 2 octets, UUID16 impair : tous ignorés ou tronqués
        var r = BleAdvertisementParser.Parse(Raw(Ad(0xFF, 0x4A), Ad(0x16, 0x26), Ad(0x03, 0x26, 0x18, 0x99)));
        Assert.Empty(r.ManufacturerData);
        Assert.Empty(r.ServiceData);
        Assert.Equal(FtmsCodec.FitnessMachineService, Assert.Single(r.ServiceUuids));
    }
}
