using ShiftBridge.Core.Trainer;

namespace ShiftBridge.Core.Tests;

public class TrainerDiscoveryRulesTests
{
    [Fact]
    public void Ftms_service_data_flags_indoor_bike()
    {
        // flags=1 (disponible), type = bit 5 (vélo d'intérieur) => 0x0020
        Assert.True(FtmsAdvertisement.TryParse([0x01, 0x20, 0x00], out var bike));
        Assert.True(bike.MachineAvailable);
        Assert.True(bike.IsIndoorBike);

        Assert.True(FtmsAdvertisement.TryParse([0x01, 0x01, 0x00], out var treadmill));   // tapis
        Assert.False(treadmill.IsIndoorBike);
        Assert.False(FtmsAdvertisement.TryParse([0x01, 0x20], out _));                    // trop court
    }

    [Fact]
    public void Service_uuids_are_classified()
    {
        var p = TrainerAdvertisementRules.FromServiceUuids(
            [FtmsCodec.FitnessMachineService, TrainerAdvertisementRules.FecOverBleService, FtmsCodec.BaseUuid(0x180D)]);
        Assert.Equal(TrainerProtocols.Ftms | TrainerProtocols.FecOverBle, p);
        Assert.Equal(TrainerProtocols.CyclingPower, TrainerAdvertisementRules.FromServiceUuids([FtmsCodec.BaseUuid(0x1818)]));
        Assert.Equal(TrainerProtocols.None, TrainerAdvertisementRules.FromServiceUuids([FtmsCodec.BaseUuid(0x180D)]));
    }

    [Theory]
    [InlineData("KICKR CORE 4FDD", true)]
    [InlineData("Tacx NEO 2T 12345", true)]
    [InlineData("Zwift Hub", true)]
    [InlineData("Elite DIRETO XR", true)]
    [InlineData("Polar H10", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Name_fallback_recognises_common_trainers(string? name, bool expected) =>
        Assert.Equal(expected, TrainerAdvertisementRules.LooksLikeTrainerName(name));

    [Fact]
    public void Compatibility_report_lists_services_properties_and_features()
    {
        var tree = new Dictionary<Guid, List<(Guid, byte)>>
        {
            [FtmsCodec.FitnessMachineService] =
            [
                (FtmsCodec.IndoorBikeData, 0x04), (FtmsCodec.ControlPoint, 0x0A),
            ],
        };
        FtmsFeatures.TryParse([.. BitConverter.GetBytes(0u), .. BitConverter.GetBytes(1u << 13)], out var f);

        var text = TrainerReport.Build("KICKR TEST", "Bluetooth", tree, f);
        Assert.Contains("KICKR TEST", text);
        Assert.Contains("0x1826", text);
        Assert.Contains("0x2AD9  écriture + indication", text);
        Assert.Contains("simulation=oui", text);

        Assert.Contains("absente", TrainerReport.DescribeBikeData([0x01, 0x00, 0x10, 0x0E]));
        Assert.Contains("36,0 km/h", TrainerReport.DescribeBikeData([0x44, 0x00, 0x10, 0x0E, 0x50, 0x00]).Replace('.', ','));
    }
}
