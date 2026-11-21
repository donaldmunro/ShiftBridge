namespace ShiftBridge.Core.Trainer;

/// <summary>Paramètres de simulation FTMS (Set Indoor Bike Simulation Parameters, opcode 0x11).</summary>
public readonly record struct SimulationParams(double WindMs, short GradeRaw, byte CrrRaw, byte CwRaw)
{
    /// <summary>Vent en 0,001 m/s tel qu'il est codé dans la commande FTMS.</summary>
    public short WindRaw => (short)Math.Clamp(Math.Round(WindMs * 1000), short.MinValue, short.MaxValue);
    /// <summary>Pente en % (le champ FTMS est en 0,01 %).</summary>
    public double GradePercent => GradeRaw * 0.01;
    /// <summary>Coefficient de roulement (unité 0,0001).</summary>
    public double Crr => CrrRaw * 0.0001;
    /// <summary>Coefficient de traînée aérodynamique en kg/m (unité 0,01).</summary>
    public double Cw => CwRaw * 0.01;
}

/// <summary>
/// Virtual shifting appliqué entre l'appli d'entraînement et le trainer : un changement de rapport est simulé en
/// modifiant la PENTE envoyée au trainer (méthode « BikeControl sets a slope »), pas en lui envoyant un rapport.
///
/// Modèle (propre à ShiftBridge, la formule de BikeControl n'est pas publique) :
///   k = rapport virtuel / rapport de référence (rapport 12 = 2,40 : le rapport physique du trainer).
///   La vitesse virtuelle vaut v_v = k · v_p (v_p = vitesse mesurée par le trainer).
///   À puissance égale, la force à appliquer au trainer est F_t = k · F_route(v_v).
///   On cherche la pente θ' telle que le modèle du trainer, à la vitesse v_p, donne cette force.
/// </summary>
public sealed class VirtualShiftingModel
{
    private const double G = 9.80665;

    /// <summary>Table par défaut (BikeControl) : 24 rapports de 0,75 à 5,49, le rapport 12 (2,40) est la référence.</summary>
    public static readonly double[] DefaultRatios =
    [
        0.75, 0.87, 0.99, 1.11, 1.23, 1.38, 1.53, 1.68, 1.86, 2.04, 2.22, 2.40,
        2.61, 2.82, 3.03, 3.24, 3.49, 3.74, 3.99, 4.24, 4.54, 4.84, 5.14, 5.49,
    ];

    public VirtualShiftingModel(double[]? ratios = null, int referenceGear = 12)
    {
        Ratios = ratios is { Length: > 0 } ? ratios : DefaultRatios;
        ReferenceGear = Math.Clamp(referenceGear, 1, Ratios.Length);
        Gear = ReferenceGear;
        _referenceRatio = Ratios[ReferenceGear - 1];
    }

    private double _referenceRatio;

    /// <summary>
    /// Rapport RÉEL du vélo sur le trainer (plateau ÷ pignon) : k = rapport virtuel ÷ <see cref="ReferenceRatio"/>.
    /// Par défaut, le rapport de référence de la table (2,40 : rapport 12). À régler d'après le braquet du coureur
    /// (voir <see cref="Drivetrain"/>) : avec un grand plateau, garder 2,40 rendrait tous les rapports trop durs.
    /// </summary>
    public double ReferenceRatio
    {
        get => _referenceRatio;
        set => _referenceRatio = Math.Clamp(value, 0.5, 8.0);
    }

    /// <summary>Table des rapports. Remplacée d'un bloc par <see cref="SetGearCount"/> : lire une seule fois (voir <see cref="RatioFor"/>).</summary>
    public double[] Ratios { get; private set; }
    public int ReferenceGear { get; }

    /// <summary>
    /// Table de <paramref name="count"/> rapports couvrant la même plage que <see cref="DefaultRatios"/> (0,75 → 5,49) :
    /// chaque rapport est pris à la même position relative de l'échelle que dans la table par défaut (interpolation
    /// logarithmique entre deux rapports voisins) : même courbe, moins de pas. Avec 24 rapports, c'est la table par défaut elle-même.
    /// </summary>
    public static double[] BuildRatios(int count)
    {
        var table = DefaultRatios;
        if (count == table.Length) return [.. table];
        if (count <= 1) return [table[table.Length / 2 - 1]];
        var result = new double[count];
        for (int i = 0; i < count; i++)
        {
            double position = (double)i / (count - 1) * (table.Length - 1);
            int lo = Math.Min((int)Math.Floor(position), table.Length - 2);
            double t = position - lo;
            result[i] = Math.Round(Math.Exp(Math.Log(table[lo]) + t * (Math.Log(table[lo + 1]) - Math.Log(table[lo]))), 2);
        }
        return result;
    }

    /// <summary>Change le nombre de rapports (table recalculée, rapport courant ramené dans la plage).</summary>
    public void SetGearCount(int count)
    {
        var table = BuildRatios(Math.Max(1, count));
        Ratios = table;
        Gear = Math.Clamp(Gear, 1, table.Length);
    }

    /// <summary>Rapport de la table pour la vitesse <paramref name="gear"/> (bornée). Lit la table une seule fois : sûr pendant un changement de nombre de vitesses.</summary>
    public double RatioFor(int gear)
    {
        var table = Ratios;
        return table[Math.Clamp(gear, 1, table.Length) - 1];
    }

    /// <summary>Rapport courant (1..N).</summary>
    public int Gear { get; set; }

    /// <summary>Masse totale coureur + vélo utilisée par le modèle (le trainer utilise la sienne ; 85 kg par défaut).</summary>
    public double TotalMassKg { get; set; } = 85;

    /// <summary>Pente corrigée limitée à cette plage, en %, pour rester dans ce que les trainers acceptent.</summary>
    public double MinGradePercent { get; set; } = -15;
    public double MaxGradePercent { get; set; } = 25;

    public double Factor => RatioFor(Gear) / ReferenceRatio;

    /// <summary>
    /// Vrai : on ne modifie QUE la pente (ancien comportement), pour les trainers qui ignoreraient le coefficient de
    /// roulement et la traînée de la commande de simulation. Faux (défaut) : mise à l'échelle complète (voir
    /// <see cref="AdjustSimulation"/>).
    /// </summary>
    public bool SlopeOnly { get; set; }

    /// <summary>
    /// Paramètres de simulation à envoyer au trainer pour émuler le rapport virtuel. Mise à l'échelle COMPLÈTE de la
    /// résistance, comme le ferait un trainer qui applique lui-même un rapport : tout se passe comme si la route était
    /// parcourue à la vitesse virtuelle v_v = k·v_p, avec une force F_t = k·F_route(v_v) :
    ///   gravité : sin θ' = k·sin θ ;  roulement : crr' = k·crr ;  traînée : cw' = k³·cw, vent' = vent / k.
    /// Ces valeurs ne dépendent pas de la vitesse mesurée : au rapport 1, la résistance tombe presque à zéro à toute
    /// vitesse (l'ancienne méthode « pente seule » devait annuler la traînée du trainer par une pente négative plafonnée).
    /// Les champs FTMS étant plafonnés (cw ≤ 2,55 kg/m) et arrondis, l'écart restant avec la force voulue est reporté sur
    /// la pente : c'est le cas des grands rapports, où la traînée voulue dépasse ce que cw peut coder.
    /// </summary>
    public SimulationParams AdjustSimulation(SimulationParams p, double physicalSpeedMs)
    {
        if (SlopeOnly) return p with { GradeRaw = AdjustGrade(p, physicalSpeedMs) };

        double k = Factor;
        if (Math.Abs(k - 1.0) < 1e-9) return p; // rapport de référence : on ne touche à rien

        double vp = Math.Max(0, physicalSpeedMs);
        double m = TotalMassKg;
        double theta = Math.Atan(p.GradePercent / 100.0);
        double sinT = Math.Sin(theta), cosT = Math.Cos(theta);

        // Pesanteur et roulement.
        double sinNew = Math.Clamp(k * sinT, -0.99, 0.99);
        double thetaNew = Math.Asin(sinNew);
        double cosNew = Math.Cos(thetaNew);
        byte crrRaw = (byte)Math.Clamp(Math.Round(k * p.Crr * cosT / cosNew / 0.0001), 0, 255);

        // Aérodynamique.
        short windRaw = (short)Math.Clamp(Math.Round(p.WindMs / k * 1000), short.MinValue, short.MaxValue);
        byte cwRaw = (byte)Math.Clamp(Math.Round(k * k * k * p.Cw / 0.01), 0, 255);

        // Force voulue (route à la vitesse virtuelle, ramenée au trainer) contre ce que le trainer appliquera avec ces
        // valeurs codées ; l'écart (arrondis, plafond de cw) passe par la pente.
        double airWanted = k * vp + p.WindMs;
        double wanted = k * (m * G * (sinT + p.Crr * cosT) + p.Cw * airWanted * Math.Abs(airWanted));
        double airTrainer = vp + windRaw * 0.001;
        double trainer = m * G * (sinNew + crrRaw * 0.0001 * cosNew) + cwRaw * 0.01 * airTrainer * Math.Abs(airTrainer);
        double sinFinal = Math.Clamp(sinNew + (wanted - trainer) / (m * G), -0.99, 0.99);

        double gradePercent = Math.Clamp(Math.Tan(Math.Asin(sinFinal)) * 100.0, MinGradePercent, MaxGradePercent);
        return new SimulationParams(windRaw * 0.001, (short)Math.Round(gradePercent * 100.0), crrRaw, cwRaw);
    }

    /// <summary>Pente (0,01 %) à envoyer réellement au trainer, en mode « pente seule ».</summary>
    public short AdjustGrade(SimulationParams p, double physicalSpeedMs)
    {
        double k = Factor;
        if (Math.Abs(k - 1.0) < 1e-9) return p.GradeRaw; // rapport de référence : on ne touche à rien

        double vp = Math.Max(0, physicalSpeedMs);
        double vv = k * vp;
        double m = TotalMassKg;

        double theta = Math.Atan(p.GradePercent / 100.0);
        double airVirtual = vv + p.WindMs;
        double fRoad = m * G * (Math.Sin(theta) + p.Crr * Math.Cos(theta)) + p.Cw * airVirtual * Math.Abs(airVirtual);
        double fTarget = k * fRoad;

        double airTrainer = vp + p.WindMs;
        double dragTrainer = p.Cw * airTrainer * Math.Abs(airTrainer);

        // sin θ' = (F_cible − traînée) / (m·g) − crr·cos θ' ; cos θ' varie peu, 3 itérations suffisent.
        double thetaNew = theta;
        for (int i = 0; i < 3; i++)
        {
            double s = (fTarget - dragTrainer) / (m * G) - p.Crr * Math.Cos(thetaNew);
            thetaNew = Math.Asin(Math.Clamp(s, -0.99, 0.99));
        }

        double gradePercent = Math.Clamp(Math.Tan(thetaNew) * 100.0, MinGradePercent, MaxGradePercent);
        return (short)Math.Round(gradePercent * 100.0);
    }
}
