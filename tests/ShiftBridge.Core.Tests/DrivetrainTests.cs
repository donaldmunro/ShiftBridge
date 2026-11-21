using ShiftBridge.Core.Config;
using ShiftBridge.Core.Trainer;

namespace ShiftBridge.Core.Tests;

public class DrivetrainTests
{
    [Fact]
    public void Ratio_and_validation()
    {
        Assert.Equal(34.0 / 14.0, Drivetrain.Default.Ratio, 6);
        Assert.Equal("34×14", Drivetrain.Default.ToString());
        Assert.True(new Drivetrain(50, 14).IsValid);
        Assert.False(new Drivetrain(10, 14).IsValid);     // plateau trop petit
        Assert.False(new Drivetrain(50, 0).IsValid);      // pignon nul : pas de division par zéro
        Assert.Equal(0, new Drivetrain(50, 0).Ratio);
    }

    [Fact]
    public void Chainring_is_proposed_from_a_measured_ratio()
    {
        Assert.Equal(34, Drivetrain.ChainringFor(2.43, 14));    // 34/14 = 2,43
        Assert.Equal(50, Drivetrain.ChainringFor(3.57, 14));    // 50/14 = 3,57
        Assert.Equal(Drivetrain.MaxChainring, Drivetrain.ChainringFor(9.0, 14));  // plafonné
    }

    [Fact]
    public void Config_roundtrips_the_drivetrain()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var path = Path.Combine(dir.FullName, "config.json");
            ConfigStore.Save(new AppConfig { ChainringTeeth = 50, CogTeeth = 16, WheelCircumferenceM = 2.096 }, path);
            var back = ConfigStore.Load(path);
            Assert.Equal(50, back.ChainringTeeth);
            Assert.Equal(16, back.CogTeeth);
            Assert.Equal(2.096, back.WheelCircumferenceM, 6);
            Assert.Equal(34, new AppConfig().ChainringTeeth);                        // valeurs par défaut
            Assert.Equal(14, new AppConfig().CogTeeth);
        }
        finally { dir.Delete(true); }
    }
}

public class ReferenceRatioTests
{
    private const double G = 9.80665;

    private static double TrainerForce(SimulationParams a, double vp, double mass)
    {
        double theta = Math.Atan(a.GradePercent / 100);
        double air = vp + a.WindMs;
        return mass * G * (Math.Sin(theta) + a.Crr * Math.Cos(theta)) + a.Cw * air * Math.Abs(air);
    }

    private static double WantedForce(SimulationParams p, double k, double vp, double mass)
    {
        double theta = Math.Atan(p.GradePercent / 100);
        double air = k * vp + p.WindMs;
        return k * (mass * G * (Math.Sin(theta) + p.Crr * Math.Cos(theta)) + p.Cw * air * Math.Abs(air));
    }

    [Fact]
    public void Default_reference_is_the_table_ratio_so_behaviour_is_unchanged()
    {
        var m = new VirtualShiftingModel { Gear = 12 };
        Assert.Equal(2.40, m.ReferenceRatio, 6);
        Assert.Equal(1.0, m.Factor, 9);
    }

    [Fact]
    public void A_bigger_real_gearing_makes_every_virtual_gear_easier()
    {
        var small = new VirtualShiftingModel { ReferenceRatio = new Drivetrain(34, 14).Ratio };
        var big = new VirtualShiftingModel { ReferenceRatio = new Drivetrain(50, 14).Ratio };

        foreach (int gear in new[] { 1, 6, 12, 18, 24 })
        {
            small.Gear = big.Gear = gear;
            Assert.True(big.Factor < small.Factor, $"rapport {gear}");
        }
        big.Gear = 12;
        Assert.Equal(2.40 / (50.0 / 14.0), big.Factor, 6);                 // ≈ 0,67 : le rapport 12 devient plus facile
        big.Gear = 1;
        Assert.True(big.Factor < 0.22);                                    // rapport 1 : ~0,21
    }

    [Fact]
    public void Reference_ratio_is_clamped_to_a_sane_range()
    {
        var m = new VirtualShiftingModel { ReferenceRatio = 0 };
        Assert.Equal(0.5, m.ReferenceRatio);
        m.ReferenceRatio = 99;
        Assert.Equal(8.0, m.ReferenceRatio);
    }

    [Fact]
    public void Trainer_force_still_equals_k_times_road_force_for_any_reference()
    {
        foreach (double reference in new[] { 2.0, 2.43, 3.57, 4.5 })
            foreach (int gear in new[] { 1, 4, 8, 12, 16 })
                foreach (double vp in new[] { 4.0, 9.0, 13.0 })
                {
                    var m = new VirtualShiftingModel { ReferenceRatio = reference, Gear = gear };
                    var p = new SimulationParams(0, 300, 40, 51);
                    var adjusted = m.AdjustSimulation(p, vp);
                    if (adjusted.GradePercent <= m.MinGradePercent + 0.05 || adjusted.GradePercent >= m.MaxGradePercent - 0.05) continue;
                    double diff = Math.Abs(TrainerForce(adjusted, vp, m.TotalMassKg) - WantedForce(p, m.Factor, vp, m.TotalMassKg));
                    Assert.True(diff < 1.0, $"référence {reference}, rapport {gear}, {vp} m/s : écart {diff:0.00} N");
                }
    }
}

public class CadenceCodecTests
{
    [Fact]
    public void Cadence_with_instantaneous_speed_present()
    {
        // flags 0x0044 : vitesse instantanée (bit0=0) + cadence (bit2) + puissance (bit6)
        byte[] data = [0x44, 0x00, 0x10, 0x0E, 0xB4, 0x00, 0x50, 0x00];   // cadence brute 180 → 90 tr/min
        Assert.True(FtmsCodec.TryGetCadenceRpm(data, out var rpm));
        Assert.Equal(90.0, rpm, 3);
    }

    [Fact]
    public void Cadence_offset_accounts_for_average_speed_field()
    {
        // flags 0x0006 : vitesse instantanée + vitesse moyenne (bit1) + cadence (bit2)
        byte[] data = [0x06, 0x00, 0x10, 0x0E, 0x00, 0x0E, 0xA0, 0x00];   // cadence brute 160 → 80 tr/min
        Assert.True(FtmsCodec.TryGetCadenceRpm(data, out var rpm));
        Assert.Equal(80.0, rpm, 3);
    }

    [Fact]
    public void Cadence_when_there_is_no_instantaneous_speed()
    {
        byte[] data = [0x05, 0x00, 0xB4, 0x00];                            // bit0=1 : pas de vitesse ; cadence en tête
        Assert.True(FtmsCodec.TryGetCadenceRpm(data, out var rpm));
        Assert.Equal(90.0, rpm, 3);
    }

    [Fact]
    public void Missing_or_truncated_cadence_is_reported_as_unavailable()
    {
        Assert.False(FtmsCodec.TryGetCadenceRpm([0x40, 0x00, 0x10, 0x0E, 0x50, 0x00], out _)); // pas de bit cadence
        Assert.False(FtmsCodec.TryGetCadenceRpm([0x44, 0x00, 0x10, 0x0E, 0xB4], out _));       // tronqué
        Assert.False(FtmsCodec.TryGetCadenceRpm([0x44], out _));
    }
}

public class RealRatioDetectorTests
{
    private const double C = 2.105;

    private static double Speed(double ratio, double rpm) => ratio * rpm / 60.0 * C;

    [Fact]
    public void Steady_pedalling_gives_the_real_ratio_within_one_percent_despite_noise()
    {
        var d = new RealRatioDetector(C);
        var rnd = new Random(42);
        for (int i = 0; i < 30; i++)
            d.AddSample(Speed(2.43, 90) * (1 + (rnd.NextDouble() - 0.5) * 0.04), 90 + (rnd.NextDouble() - 0.5) * 2);

        Assert.True(d.TryGetResult(out var ratio, out var n, out var spread));
        Assert.Equal(2.43, ratio, 1);
        Assert.InRange(ratio / 2.43, 0.99, 1.01);
        Assert.Equal(30, n);
        Assert.True(spread < RealRatioDetector.MaxSpread);
    }

    [Fact]
    public void Samples_outside_the_cadence_band_or_too_slow_are_ignored()
    {
        var d = new RealRatioDetector(C);
        for (int i = 0; i < 20; i++) d.AddSample(Speed(2.43, 50), 50);     // trop lent (< 60 tr/min)
        for (int i = 0; i < 20; i++) d.AddSample(Speed(2.43, 125), 125);   // trop vite (> 110 tr/min)
        for (int i = 0; i < 20; i++) d.AddSample(0.5, 90);                  // vitesse trop faible
        Assert.Equal(60, d.TotalSamples);
        Assert.Equal(0, d.AcceptedSamples);
        Assert.False(d.TryGetResult(out _, out _, out _));
        Assert.True(d.SawCadence);
    }

    [Fact]
    public void Band_edges_are_accepted()
    {
        var d = new RealRatioDetector(C);
        for (int i = 0; i < 6; i++) d.AddSample(Speed(2.43, 60), 60);
        for (int i = 0; i < 6; i++) d.AddSample(Speed(2.43, 110), 110);
        Assert.Equal(12, d.AcceptedSamples);
        Assert.True(d.TryGetResult(out var ratio, out _, out _));
        Assert.Equal(2.43, ratio, 2);
    }

    [Fact]
    public void Too_short_a_measurement_is_refused()
    {
        var d = new RealRatioDetector(C);
        for (int i = 0; i < RealRatioDetector.MinAccepted - 1; i++) d.AddSample(Speed(2.43, 90), 90);
        Assert.False(d.TryGetResult(out _, out _, out _));
    }

    [Fact]
    public void A_single_wild_sample_does_not_fool_the_median()
    {
        var d = new RealRatioDetector(C);
        for (int i = 0; i < 15; i++) d.AddSample(Speed(2.43, 90), 90);
        d.AddSample(Speed(6.0, 90), 90);                                   // valeur aberrante (ex. perte de paquet)
        for (int i = 0; i < 15; i++) d.AddSample(Speed(2.43, 90), 90);

        Assert.True(d.TryGetResult(out var ratio, out _, out var spread));
        Assert.Equal(2.43, ratio, 2);
        Assert.True(spread < 0.01);
    }

    [Fact]
    public void An_unstable_ratio_is_not_reported()
    {
        var d = new RealRatioDetector(C);
        for (int i = 0; i < 40; i++) d.AddSample(Speed(i % 2 == 0 ? 2.0 : 3.0, 90), 90);
        Assert.False(d.TryGetResult(out _, out _, out _));
    }

    [Fact]
    public void A_chainring_change_during_the_measurement_restarts_from_the_new_ratio()
    {
        var d = new RealRatioDetector(C);
        for (int i = 0; i < 20; i++) d.AddSample(Speed(2.43, 90), 90);
        for (int i = 0; i < 20; i++) d.AddSample(Speed(3.57, 90), 90);     // l'utilisateur passe au grand plateau
        Assert.True(d.TryGetResult(out var ratio, out _, out _));
        Assert.Equal(3.57, ratio, 1);
    }

    [Fact]
    public void Reset_clears_everything()
    {
        var d = new RealRatioDetector(C);
        for (int i = 0; i < 20; i++) d.AddSample(Speed(2.43, 90), 90);
        d.Reset();
        Assert.Equal(0, d.TotalSamples);
        Assert.False(d.TryGetResult(out _, out _, out _));
    }
}
