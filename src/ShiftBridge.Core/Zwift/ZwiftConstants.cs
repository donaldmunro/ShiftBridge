namespace ShiftBridge.Core.Zwift;

/// <summary>
/// Constantes du protocole BLE des manettes Zwift (rétro-ingénierie publique : qdomyos-zwift, ajchellew/zwiftplay,
/// BikeControl tag gpl3). Aucune de ces valeurs n'est officielle.
/// </summary>
public static class ZwiftConstants
{
    public const ushort ManufacturerId = 0x094A; // 2378

    public static readonly Guid CustomService = new("00000001-19CA-4651-86E5-FA29DCDD09D1");
    public static readonly Guid RideCustomService = new("0000fc82-0000-1000-8000-00805f9b34fb");
    public static readonly Guid AsyncCharacteristic = new("00000002-19CA-4651-86E5-FA29DCDD09D1");   // notify
    public static readonly Guid SyncRxCharacteristic = new("00000003-19CA-4651-86E5-FA29DCDD09D1");  // write
    public static readonly Guid SyncTxCharacteristic = new("00000004-19CA-4651-86E5-FA29DCDD09D1");  // indicate

    /// <summary>« RideOn » en ASCII : ouvre la session (mode non chiffré).</summary>
    public static readonly byte[] RideOn = [0x52, 0x69, 0x64, 0x65, 0x4F, 0x6E];

    // Premier octet des notifications
    public const byte MsgPlayNotification = 0x07;
    public const byte MsgEmpty = 0x15;
    public const byte MsgBattery = 0x19;
    public const byte MsgBatteryRide = 0x1A;
    public const byte MsgRideNotification = 0x23;
    public const byte MsgClickNotification = 0x37;
    public const byte MsgDisconnect = 0xFE;

    // Seuil de la palette analogique du Ride (|valeur|) : 25 chez BikeControl, à valider sur Ride réel.
    public const int AnalogPaddleThreshold = 25;

    // Play : le champ 8 reste autour de ±25 au repos (capture réelle : 0x2F/0x33/0x37 => -24/-26/-28)
    // et atteint ±100 à fond. Seuil à 90 pour ignorer le bruit.
    public const int PlayAnalogThreshold = 90;

    /// <summary>Commande de vibration : 12 12 08 0A 06 08 02 10 00 18 &lt;motif&gt;.</summary>
    public static byte[] VibrationCommand(byte pattern) =>
        [0x12, 0x12, 0x08, 0x0A, 0x06, 0x08, 0x02, 0x10, 0x00, 0x18, pattern];
}

/// <summary>Masques du champ buttonMap (Ride / Click v2). Un bit à 0 signifie « appuyé ».</summary>
public static class RideButtonMask
{
    public const uint Left = 0x00001, Up = 0x00002, Right = 0x00004, Down = 0x00008;
    public const uint A = 0x00010, B = 0x00020, Y = 0x00040, Z = 0x00080;
    public const uint ShiftUpLeft = 0x00100, ShiftDownLeft = 0x00200, PowerUpLeft = 0x00400, OnOffLeft = 0x00800;
    public const uint ShiftUpRight = 0x01000, ShiftDownRight = 0x02000, PowerUpRight = 0x04000, OnOffRight = 0x08000;
}
