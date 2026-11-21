using ShiftBridge.Core.Trainer;

namespace ShiftBridge.Core.Tests;

public class FtmsCodecTests
{
    [Fact]
    public void Simulation_parameters_roundtrip_and_grade_rewrite()
    {
        // vent 0, pente +5,00 % (500 = 0x01F4), crr 0,0040 (40), cw 0,51 (51)
        byte[] cp = [0x11, 0x00, 0x00, 0xF4, 0x01, 40, 51];
        Assert.True(FtmsCodec.TryParseSimulation(cp, out var p));
        Assert.Equal(5.0, p.GradePercent, 3);
        Assert.Equal(0.0040, p.Crr, 6);
        Assert.Equal(0.51, p.Cw, 6);

        var rewritten = FtmsCodec.WithGrade(cp, -250); // -2,50 %
        Assert.True(FtmsCodec.TryParseSimulation(rewritten, out var q));
        Assert.Equal(-2.5, q.GradePercent, 3);
        Assert.Equal(cp[5], rewritten[5]);
        Assert.Equal(cp[6], rewritten[6]);
    }

    [Fact]
    public void Other_control_point_opcodes_are_not_simulation()
    {
        Assert.False(FtmsCodec.TryParseSimulation([0x05, 0xC8, 0x00], out _)); // Set Target Power
        Assert.False(FtmsCodec.TryParseSimulation([0x11, 0, 0], out _));       // trop court
    }

    [Fact]
    public void Indoor_bike_data_speed_is_read_and_scaled()
    {
        // flags = 0x0044 (bit 0 = 0 : vitesse présente), vitesse = 3600 => 36,00 km/h = 10 m/s
        byte[] data = [0x44, 0x00, 0x10, 0x0E, 0x50, 0x00];
        Assert.True(FtmsCodec.TryGetSpeedMs(data, out var v));
        Assert.Equal(10.0, v, 3);

        var scaled = FtmsCodec.ScaleSpeed(data, 2.0);
        Assert.True(FtmsCodec.TryGetSpeedMs(scaled, out var v2));
        Assert.Equal(20.0, v2, 3);
        Assert.Equal(data[4], scaled[4]); // le reste de la trame est intact
    }

    [Fact]
    public void Indoor_bike_data_without_speed_field_is_untouched()
    {
        byte[] data = [0x01, 0x00, 0x10, 0x0E]; // bit 0 = 1 : « more data », pas de vitesse instantanée
        Assert.False(FtmsCodec.TryGetSpeedMs(data, out _));
        Assert.Equal(data, FtmsCodec.ScaleSpeed(data, 3.0));
    }

    [Fact]
    public void Control_point_response_detection()
    {
        Assert.True(FtmsCodec.IsResponseTo([0x80, 0x11, 0x01], 0x11));
        Assert.False(FtmsCodec.IsResponseTo([0x80, 0x00, 0x01], 0x11));
    }
}

public class VirtualShiftingModelTests
{
    private static SimulationParams Flat => new(0, 0, 40, 51); // 0 %, crr 0,004, cw 0,51

    [Fact]
    public void Reference_gear_does_not_change_the_grade()
    {
        var m = new VirtualShiftingModel { Gear = 12 };
        Assert.Equal(300, m.AdjustGrade(new SimulationParams(0, 300, 40, 51), 8.0));
    }

    [Fact]
    public void Default_table_matches_published_values()
    {
        Assert.Equal(24, VirtualShiftingModel.DefaultRatios.Length);
        Assert.Equal(0.75, VirtualShiftingModel.DefaultRatios[0]);
        Assert.Equal(2.40, VirtualShiftingModel.DefaultRatios[11]);
        Assert.Equal(5.49, VirtualShiftingModel.DefaultRatios[23]);
        Assert.Equal(5.49 / 2.40, new VirtualShiftingModel { Gear = 24 }.Factor, 6);
    }

    [Fact]
    public void Bigger_gear_makes_it_harder_and_smaller_gear_easier_on_the_same_road()
    {
        var m = new VirtualShiftingModel();
        double speed = 8.0;
        m.Gear = 12; short reference = m.AdjustGrade(Flat, speed);
        m.Gear = 18; short harder = m.AdjustGrade(Flat, speed);
        m.Gear = 6; short easier = m.AdjustGrade(Flat, speed);

        Assert.True(harder > reference, $"gear 18 : {harder} doit dépasser {reference}");
        Assert.True(easier < reference, $"gear 6 : {easier} doit être inférieur à {reference}");
    }

    [Fact]
    public void Adjusted_trainer_force_matches_k_times_road_force()
    {
        // Vérifie l'identité physique : force du trainer (pente corrigée, vitesse physique) = k · force route (vitesse virtuelle).
        const double g = 9.80665;
        var m = new VirtualShiftingModel { Gear = 20, TotalMassKg = 85 };
        var p = new SimulationParams(0, 400, 40, 51); // +4 %
        double vp = 7.0, k = m.Factor, vv = k * vp;

        short raw = m.AdjustGrade(p, vp);
        double thetaNew = Math.Atan(raw * 0.0001);
        double fTrainer = 85 * g * (Math.Sin(thetaNew) + p.Crr * Math.Cos(thetaNew)) + p.Cw * vp * vp;

        double theta = Math.Atan(p.GradePercent / 100);
        double fRoad = 85 * g * (Math.Sin(theta) + p.Crr * Math.Cos(theta)) + p.Cw * vv * vv;

        Assert.Equal(k * fRoad, fTrainer, 0); // à ±0,5 N (arrondi de la pente à 0,01 %)
    }

    // ---- Mise à l'échelle complète (mode par défaut) ----

    private const double Gr = 9.80665;

    /// <summary>Force que le trainer applique avec les paramètres envoyés, à sa vitesse physique.</summary>
    private static double TrainerForce(SimulationParams a, double vp, double mass)
    {
        double theta = Math.Atan(a.GradePercent / 100);
        double air = vp + a.WindMs;
        return mass * Gr * (Math.Sin(theta) + a.Crr * Math.Cos(theta)) + a.Cw * air * Math.Abs(air);
    }

    /// <summary>Force voulue : k · force de la route à la vitesse virtuelle k·v_p.</summary>
    private static double WantedForce(SimulationParams p, double k, double vp, double mass)
    {
        double theta = Math.Atan(p.GradePercent / 100);
        double air = k * vp + p.WindMs;
        return k * (mass * Gr * (Math.Sin(theta) + p.Crr * Math.Cos(theta)) + p.Cw * air * Math.Abs(air));
    }

    [Fact]
    public void Full_scaling_matches_the_wanted_force_across_gears_speeds_and_grades()
    {
        var m = new VirtualShiftingModel();
        foreach (int gear in new[] { 1, 3, 6, 9, 14, 17, 20 })
            foreach (double vp in new[] { 3.0, 6.0, 9.0, 12.0 })
                foreach (short grade in new short[] { -200, 0, 300, 800 })
                {
                    m.Gear = gear;
                    var p = new SimulationParams(0, grade, 40, 51);
                    var adjusted = m.AdjustSimulation(p, vp);

                    // Hors plafond de pente : la force du trainer doit égaler k · F_route(k·v_p) (à 1 N près, arrondis FTMS compris).
                    if (adjusted.GradePercent <= m.MinGradePercent + 0.05 || adjusted.GradePercent >= m.MaxGradePercent - 0.05) continue;
                    Assert.True(Math.Abs(TrainerForce(adjusted, vp, m.TotalMassKg) - WantedForce(p, m.Factor, vp, m.TotalMassKg)) < 1.0,
                        $"rapport {gear}, {vp} m/s, pente {grade / 100.0} % : {TrainerForce(adjusted, vp, m.TotalMassKg):0.00} N au lieu de {WantedForce(p, m.Factor, vp, m.TotalMassKg):0.00} N");
                }
    }

    [Fact]
    public void Gear_1_is_nearly_free_at_any_speed_without_relying_on_the_measured_speed()
    {
        var m = new VirtualShiftingModel { Gear = 1 };
        var flat = new SimulationParams(0, 0, 40, 51);

        var slow = m.AdjustSimulation(flat, 4.0);
        var fast = m.AdjustSimulation(flat, 14.0);

        // Traînée et roulement ramenés quasi à zéro : le trainer n'oppose presque rien, quelle que soit la vitesse.
        Assert.True(slow.Cw <= 0.03 && fast.Cw <= 0.03, $"traînée {slow.Cw}");
        Assert.True(slow.Crr < 0.002);
        double freeForce = TrainerForce(fast, 14.0, m.TotalMassKg);
        Assert.True(freeForce < 6.0, $"{freeForce:0.0} N à 50 km/h au rapport 1");           // contre ~100 N de traînée native à cette vitesse

        // La pente ne dépend (presque) plus de la vitesse mesurée : écart < 0,2 % entre 4 et 14 m/s.
        Assert.True(Math.Abs(slow.GradePercent - fast.GradePercent) < 0.2, $"{slow.GradePercent} vs {fast.GradePercent}");

        // À comparer avec l'ancienne méthode, qui devait annuler la traînée du trainer par une pente très négative.
        m.SlopeOnly = true;
        var old = m.AdjustSimulation(flat, 14.0);
        Assert.True(old.GradePercent < -5, $"pente seule : {old.GradePercent} %");
        Assert.Equal(flat.CwRaw, old.CwRaw);                                                  // et elle laisse la traînée du trainer intacte
    }

    [Fact]
    public void Reference_gear_leaves_every_parameter_untouched_in_full_mode()
    {
        var m = new VirtualShiftingModel { Gear = 12 };
        var p = new SimulationParams(1.5, 350, 40, 51);
        Assert.Equal(p, m.AdjustSimulation(p, 9.0));
    }

    [Fact]
    public void Top_gear_exceeds_the_drag_the_protocol_can_encode_and_the_rest_goes_through_the_grade()
    {
        var m = new VirtualShiftingModel { Gear = 24 };
        var flat = new SimulationParams(0, 0, 40, 51);
        var a = m.AdjustSimulation(flat, 7.6);

        Assert.Equal(255, a.CwRaw);                      // plafond FTMS (2,55 kg/m) atteint
        Assert.True(a.GradePercent > 10, $"{a.GradePercent} %");   // le reste de la traînée voulue passe par la pente
        Assert.True(a.CrrRaw > 40);                       // roulement augmenté
    }

    [Fact]
    public void Slope_only_mode_changes_only_the_grade()
    {
        var m = new VirtualShiftingModel { Gear = 18, SlopeOnly = true };
        var p = new SimulationParams(0.5, 200, 40, 51);
        var a = m.AdjustSimulation(p, 8.0);
        Assert.Equal(p.WindMs, a.WindMs);
        Assert.Equal(p.CrrRaw, a.CrrRaw);
        Assert.Equal(p.CwRaw, a.CwRaw);
        Assert.NotEqual(p.GradeRaw, a.GradeRaw);
    }

    [Fact]
    public void Wind_is_divided_by_the_factor_so_the_air_speed_term_stays_consistent()
    {
        var m = new VirtualShiftingModel { Gear = 6 };          // k < 1
        var p = new SimulationParams(2.0, 0, 40, 51);           // vent de face 2 m/s
        var a = m.AdjustSimulation(p, 8.0);
        Assert.True(a.WindMs > p.WindMs);                        // vent / k > vent
        Assert.True(Math.Abs(TrainerForce(a, 8.0, 85) - WantedForce(p, m.Factor, 8.0, 85)) < 1.0);
    }

    [Fact]
    public void Simulation_codec_writes_every_field()
    {
        byte[] cp = [0x11, 0, 0, 0, 0, 40, 51];
        var bytes = FtmsCodec.WithSimulation(cp, new SimulationParams(-1.234, -150, 12, 2));
        Assert.True(FtmsCodec.TryParseSimulation(bytes, out var p));
        Assert.Equal(-1.234, p.WindMs, 3);
        Assert.Equal(-150, p.GradeRaw);
        Assert.Equal(12, p.CrrRaw);
        Assert.Equal(2, p.CwRaw);
    }

    [Fact]
    public void Grade_is_clamped_to_the_allowed_range()
    {
        var m = new VirtualShiftingModel { Gear = 24 };
        short raw = m.AdjustGrade(new SimulationParams(0, 2000, 40, 51), 15.0); // montée raide + grand rapport
        Assert.InRange(raw, (short)(m.MinGradePercent * 100), (short)(m.MaxGradePercent * 100));
    }

    [Fact]
    public void Standing_still_still_produces_a_valid_grade()
    {
        var m = new VirtualShiftingModel { Gear = 20 };
        short raw = m.AdjustGrade(Flat, 0);
        Assert.InRange(raw, (short)-1500, (short)2500);
    }
}
