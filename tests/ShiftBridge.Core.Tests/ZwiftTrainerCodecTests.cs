using ShiftBridge.Core.Protobuf;
using ShiftBridge.Core.Trainer;
using ShiftBridge.Core.Zwift;

namespace ShiftBridge.Core.Tests;

public class ProtoWriterTests
{
    [Fact]
    public void Writer_and_reader_roundtrip_varints_zigzag_and_nested_messages()
    {
        var bytes = new ProtoWriter()
            .Varint(1, 300)                                  // varint sur 2 octets
            .SInt(2, -250)                                   // zigzag négatif
            .Message(3, new ProtoWriter().Varint(1, 7).SInt(2, 400))
            .ToArray();

        var fields = ProtoReader.ReadFields(bytes);
        Assert.Equal(3, fields.Count);
        Assert.Equal(300UL, fields[0].Varint);
        Assert.Equal(-250, fields[1].AsZigZag());

        var inner = ProtoReader.ReadFields(fields[2].Bytes.Span);
        Assert.Equal(7UL, inner[0].Varint);
        Assert.Equal(400, inner[1].AsZigZag());
    }

    [Theory]
    [InlineData(0L, 0UL)]
    [InlineData(-1L, 1UL)]
    [InlineData(1L, 2UL)]
    [InlineData(400L, 800UL)]
    [InlineData(-400L, 799UL)]
    public void ZigZag_matches_the_protobuf_definition(long value, ulong expected) =>
        Assert.Equal(expected, ProtoWriter.ZigZag(value));
}

public class ZwiftTrainerCodecTests
{
    private static string Hex(byte[] b) => Convert.ToHexString(b);

    [Fact]
    public void Fixed_frames()
    {
        Assert.Equal("52696465 4F6E0201".Replace(" ", ""), Hex(ZwiftTrainerCodec.RideOnTrainer));  // « RideOn » + 02 01
        Assert.Equal("410805", Hex(ZwiftTrainerCodec.InitFrame));
        Assert.Equal("000800", Hex(ZwiftTrainerCodec.InfoRequest));
        Assert.Equal("00088804", Hex(ZwiftTrainerCodec.GearQuery));                              // paramètre 520
    }

    [Fact]
    public void Simulation_frame_for_a_4_percent_grade_with_zwift_default_coefficients()
    {
        // grade 4,00 % (400), cw 0,51 (51), crr 0,004 (40), pas de vent : CWa 5100, Crr 400
        var frame = ZwiftTrainerCodec.EncodeSimulation(new SimulationParams(0, 400, 40, 51), full: true);
        Assert.Equal("04220B0800 10A006 18EC27 209003".Replace(" ", ""), Hex(frame));
    }

    [Fact]
    public void Simulation_frame_for_5_percent()
    {
        var frame = ZwiftTrainerCodec.EncodeSimulation(new SimulationParams(0, 500, 40, 51), full: true);
        Assert.Equal("04220B0800 10E807 18EC27 209003".Replace(" ", ""), Hex(frame));
    }

    [Fact]
    public void Keep_alive_simulation_carries_only_wind_and_grade()
    {
        var frame = ZwiftTrainerCodec.EncodeSimulation(new SimulationParams(0, 400, 40, 51), full: false);
        Assert.Equal("0422050800 10A006".Replace(" ", ""), Hex(frame));
    }

    [Fact]
    public void Negative_grade_and_wind_use_zigzag_and_the_unit_conversions()
    {
        // pente −2,50 % (−250 → zigzag 499 = F3 03) ; vent de face 1,5 m/s = 1500 brut FTMS → 150 (×100) → zigzag 300 = AC 02
        var frame = ZwiftTrainerCodec.EncodeSimulation(new SimulationParams(1.5, -250, 40, 51), full: false);
        var inner = ProtoReader.ReadFields(ProtoReader.ReadFields(frame.AsSpan(1))[0].Bytes.Span);
        Assert.Equal(150, inner[0].AsZigZag());
        Assert.Equal(-250, inner[1].AsZigZag());
    }

    [Fact]
    public void Physical_frame_with_gear_and_weights()
    {
        // rapport 2,4 → 24000 (C0 BB 01) ; vélo 8 kg → 800 (A0 06) ; coureur 80 kg → 8000 (C0 3E)
        Assert.Equal("042A0A10C0BB0120A00628C03E", Hex(ZwiftTrainerCodec.EncodePhysical(2.4, 8, 80)));
    }

    [Fact]
    public void Physical_frame_with_gear_only_and_the_release_frame()
    {
        Assert.Equal("042A0410F49602", Hex(ZwiftTrainerCodec.EncodePhysical(3.57)));            // rapport 3,57 → 35700
        Assert.Equal("042A021000", Hex(ZwiftTrainerCodec.EncodePhysical(0)));                   // rapport 0 : libère le virtual shifting
    }

    [Fact]
    public void Physical_frame_matches_the_frame_captured_by_makinolo_for_weights_only()
    {
        // 04 2A 08 10 00 20 81 05 28 C4 3B : rapport 0, vélo 6,41 kg, coureur 76,2 kg
        Assert.Equal("042A0810002081052 8C43B".Replace(" ", ""), Hex(ZwiftTrainerCodec.EncodePhysical(0, 6.41, 76.2)));
    }

    [Fact]
    public void Riding_data_is_decoded()
    {
        var data = Convert.FromHexString("0308BE0110501 8BD062000 28E2BA01308BEB01".Replace(" ", ""));
        Assert.True(ZwiftTrainerCodec.TryDecodeRidingData(data, out var ride));
        Assert.Equal(190u, ride.Power);
        Assert.Equal(80u, ride.Cadence);
        Assert.Equal(829u, ride.SpeedX100);
        Assert.Equal(0u, ride.HeartRate);
        Assert.False(ZwiftTrainerCodec.TryDecodeRidingData([0x04, 0x08, 0x01], out _));        // autre opcode
    }

    // ---- Trames RÉELLES capturées sur un Wahoo KICKR (Bluetooth), journal de l'utilisateur ----

    [Fact]
    public void Real_kickr_rideon_reply_is_recognised()
    {
        Assert.True(ZwiftTrainerCodec.IsRideOnReply(Convert.FromHexString("526964654F6E0203")));   // « RideOn » + 02 03
    }

    [Fact]
    public void Real_kickr_gear_reply_is_decoded_as_ratio_2_40()
    {
        Assert.True(ZwiftTrainerCodec.TryDecodeGearReply(Convert.FromHexString("3C08880412060A0440C0BB01"), out var ratio));
        Assert.Equal(2.40, ratio, 6);
    }

    [Fact]
    public void Other_trainer_replies_are_not_mistaken_for_a_gear_reply()
    {
        Assert.False(ZwiftTrainerCodec.TryDecodeGearReply(Convert.FromHexString("3E08411001"), out _));            // accusé de 41 08 05
        Assert.False(ZwiftTrainerCodec.TryDecodeGearReply(
            Convert.FromHexString("3C080012170A153A0137480250015A0C444530433141443444393133"), out _));            // infos : pas le paramètre 520
        Assert.False(ZwiftTrainerCodec.TryDecodeGearReply([0x3C], out _));
        Assert.False(ZwiftTrainerCodec.TryDecodeGearReply(Convert.FromHexString("0308BE01"), out _));            // autre opcode
    }

    [Fact]
    public void Real_kickr_riding_data_at_rest_decodes_to_zeros()
    {
        Assert.True(ZwiftTrainerCodec.TryDecodeRidingData(Convert.FromHexString("030800100018002000280030F2C801"), out var ride));
        Assert.Equal(0u, ride.Power);
        Assert.Equal(0u, ride.Cadence);
        Assert.Equal(0u, ride.SpeedX100);
    }

    [Fact]
    public void RideOn_reply_detection_and_service_ids()
    {
        Assert.True(ZwiftTrainerCodec.IsRideOnReply([0x52, 0x69, 0x64, 0x65, 0x4F, 0x6E, 0x01, 0x03]));
        Assert.False(ZwiftTrainerCodec.IsRideOnReply([0x52, 0x69]));
        Assert.True(ZwiftTrainerCodec.IsZwiftService(ZwiftConstants.CustomService));
        Assert.True(ZwiftTrainerCodec.IsZwiftService(ZwiftConstants.RideCustomService));
        Assert.False(ZwiftTrainerCodec.IsZwiftService(FtmsCodec.FitnessMachineService));
    }

    [Fact]
    public void The_zwift_service_is_never_relayed_to_the_app_even_in_full_relay()
    {
        Assert.False(new GattRelayPolicy(full: true).ShouldRelayService(ZwiftConstants.CustomService));
        Assert.False(new GattRelayPolicy(full: false).ShouldRelayService(ZwiftConstants.RideCustomService));
        Assert.True(new GattRelayPolicy(full: true).ShouldRelayService(FtmsCodec.FitnessMachineService));
    }
}
