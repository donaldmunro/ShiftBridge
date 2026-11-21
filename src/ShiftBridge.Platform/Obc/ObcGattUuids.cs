namespace ShiftBridge.Platform.Obc;

/// <summary>UUID GATT du service OpenBikeControl (utilisés par l'OBC Bluetooth, quelle que soit la plateforme, et par DirCon).</summary>
public static class ObcGattUuids
{
    public static readonly Guid Service = new("d273f680-d548-419d-b9d1-fa0472345229");
    public static readonly Guid ButtonState = new("d273f681-d548-419d-b9d1-fa0472345229");
    public static readonly Guid Haptic = new("d273f682-d548-419d-b9d1-fa0472345229");
    public static readonly Guid AppInfo = new("d273f683-d548-419d-b9d1-fa0472345229");
}
