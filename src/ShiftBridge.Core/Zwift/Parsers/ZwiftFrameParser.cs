using ShiftBridge.Core.Input;
using ShiftBridge.Core.Protobuf;

namespace ShiftBridge.Core.Zwift.Parsers;

/// <summary>Résultat de l'analyse d'une notification : boutons appuyés, ou niveau de batterie.</summary>
public sealed record ZwiftFrame(HashSet<PhysicalButton>? Pressed, int? BatteryPercent)
{
    public static ZwiftFrame Buttons(HashSet<PhysicalButton> pressed) => new(pressed, null);
    public static ZwiftFrame Battery(int percent) => new(null, percent);
}

/// <summary>
/// Analyse les notifications des caractéristiques Async/SyncTx. Retourne null pour les trames ignorées
/// (vides, inconnues, corrompues) : l'appelant les journalise simplement dans le Diagnostic.
/// </summary>
public static class ZwiftFrameParser
{
    public static ZwiftFrame? Parse(ZwiftDeviceType device, ReadOnlySpan<byte> data)
    {
        if (data.Length == 0) return null;
        byte type = data[0];
        var body = data[1..];

        switch (type)
        {
            case ZwiftConstants.MsgBattery or ZwiftConstants.MsgBatteryRide:
                // Protobuf : champ 2 = niveau (capture réelle « 19 10 45 » => 0x45 = 69 %).
                foreach (var f in ProtoReader.ReadFields(body))
                    if (f.Number == 2 && f.WireType == ProtoField.WireVarint && f.Varint <= 100)
                        return ZwiftFrame.Battery((int)f.Varint);
                return null;

            case ZwiftConstants.MsgPlayNotification:
                return ZwiftFrame.Buttons(ParsePlay(body, device.Side));

            case ZwiftConstants.MsgClickNotification:
                return ZwiftFrame.Buttons(ParseClick(body));

            case ZwiftConstants.MsgRideNotification:
                return ZwiftFrame.Buttons(ParseRide(body));

            default:
                return null;
        }
    }

    /// <summary>
    /// Play : champs varint, valeur 0 = appuyé (BTN_PRESSED). Champ 1 = manette (0 = droite).
    /// Champs 2..5 = Y/haut, Z/gauche, A/droite, B/bas ; 6 = bouton latéral (vitesse) ; 7 = on/off ; 8 = palette (zigzag).
    /// </summary>
    internal static HashSet<PhysicalButton> ParsePlay(ReadOnlySpan<byte> body, ZwiftSide deviceSide = ZwiftSide.None)
    {
        var fields = ProtoReader.ReadFields(body);
        // Le côté vient de l'annonce BLE quand il est connu (plus fiable que le champ 1) ;
        // sinon on se rabat sur le champ 1 (0 = droite).
        bool right = true;
        if (deviceSide != ZwiftSide.None) right = deviceSide == ZwiftSide.Right;
        else
            foreach (var f in fields)
                if (f.Number == 1 && f.WireType == ProtoField.WireVarint) right = f.Varint == 0;

        var pressed = new HashSet<PhysicalButton>();
        foreach (var f in fields)
        {
            if (f.WireType != ProtoField.WireVarint) continue;
            bool down = f.Varint == 0;
            switch (f.Number)
            {
                case 2 when down: pressed.Add(right ? PhysicalButton.RightY : PhysicalButton.LeftUp); break;
                case 3 when down: pressed.Add(right ? PhysicalButton.RightZ : PhysicalButton.LeftLeft); break;
                case 4 when down: pressed.Add(right ? PhysicalButton.RightA : PhysicalButton.LeftRight); break;
                case 5 when down: pressed.Add(right ? PhysicalButton.RightB : PhysicalButton.LeftDown); break;
                // Champ 6 = bouton latéral (levier de vitesse) : confirmé sur Play réel (le bouton latéral droit
                // allume le champ 6). Convention Zwift : droite = vitesse +, gauche = vitesse −.
                case 6 when down: pressed.Add(right ? PhysicalButton.RightShiftUp : PhysicalButton.LeftShiftDown); break;
                // Champ 7 = bouton On/Off.
                case 7 when down: pressed.Add(right ? PhysicalButton.RightOnOff : PhysicalButton.LeftOnOff); break;
                case 8 when Math.Abs(f.AsZigZag()) >= ZwiftConstants.PlayAnalogThreshold:
                    pressed.Add(right ? PhysicalButton.RightPaddle : PhysicalButton.LeftPaddle); break;
            }
        }
        return pressed;
    }

    /// <summary>
    /// Click v1 : deux varints. Hypothèse (à valider sur matériel) : champ 1 = « + », champ 2 = « − », 0 = appuyé.
    /// </summary>
    internal static HashSet<PhysicalButton> ParseClick(ReadOnlySpan<byte> body)
    {
        var pressed = new HashSet<PhysicalButton>();
        foreach (var f in ProtoReader.ReadFields(body))
        {
            if (f.WireType != ProtoField.WireVarint || f.Varint != 0) continue;
            if (f.Number == 1) pressed.Add(PhysicalButton.ClickPlus);
            else if (f.Number == 2) pressed.Add(PhysicalButton.ClickMinus);
        }
        return pressed;
    }

    /// <summary>
    /// Ride / Click v2 : champ 1 = buttonMap (actif bas), champs 2 et 3 = groupes analogiques
    /// {1: emplacement (0 gauche, 1 droite), 2: valeur zigzag}.
    /// </summary>
    internal static HashSet<PhysicalButton> ParseRide(ReadOnlySpan<byte> body)
    {
        var pressed = new HashSet<PhysicalButton>();
        foreach (var f in ProtoReader.ReadFields(body))
        {
            if (f.Number == 1 && f.WireType == ProtoField.WireVarint)
            {
                uint map = (uint)f.Varint;
                void Check(uint mask, PhysicalButton b) { if ((map & mask) == 0) pressed.Add(b); }
                Check(RideButtonMask.Left, PhysicalButton.LeftLeft);
                Check(RideButtonMask.Up, PhysicalButton.LeftUp);
                Check(RideButtonMask.Right, PhysicalButton.LeftRight);
                Check(RideButtonMask.Down, PhysicalButton.LeftDown);
                Check(RideButtonMask.A, PhysicalButton.RightA);
                Check(RideButtonMask.B, PhysicalButton.RightB);
                Check(RideButtonMask.Y, PhysicalButton.RightY);
                Check(RideButtonMask.Z, PhysicalButton.RightZ);
                Check(RideButtonMask.ShiftUpLeft, PhysicalButton.LeftShiftUp);
                Check(RideButtonMask.ShiftDownLeft, PhysicalButton.LeftShiftDown);
                Check(RideButtonMask.PowerUpLeft, PhysicalButton.LeftPowerUp);
                Check(RideButtonMask.OnOffLeft, PhysicalButton.LeftOnOff);
                Check(RideButtonMask.ShiftUpRight, PhysicalButton.RightShiftUp);
                Check(RideButtonMask.ShiftDownRight, PhysicalButton.RightShiftDown);
                Check(RideButtonMask.PowerUpRight, PhysicalButton.RightPowerUp);
                Check(RideButtonMask.OnOffRight, PhysicalButton.RightOnOff);
            }
            else if ((f.Number == 2 || f.Number == 3) && f.WireType == ProtoField.WireLengthDelimited)
            {
                long? location = null, value = null;
                foreach (var sub in ProtoReader.ReadFields(f.Bytes.Span))
                {
                    if (sub.WireType != ProtoField.WireVarint) continue;
                    if (sub.Number == 1) location = (long)sub.Varint;
                    else if (sub.Number == 2) value = sub.AsZigZag();
                }
                if (location is not null && value is not null
                    && Math.Abs(value.Value) >= ZwiftConstants.AnalogPaddleThreshold)
                {
                    if (location == 0) pressed.Add(PhysicalButton.LeftPaddle);
                    else if (location == 1) pressed.Add(PhysicalButton.RightPaddle);
                }
            }
        }
        return pressed;
    }
}
