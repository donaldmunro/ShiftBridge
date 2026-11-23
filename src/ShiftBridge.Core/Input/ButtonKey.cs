using System.Text.Json;
using ShiftBridge.Core.Controllers;
using System.Text.Json.Serialization;

namespace ShiftBridge.Core.Input;

/// <summary>
/// Identité ouverte d'un bouton, quelle que soit sa source.
/// Manettes Zwift : nom de <see cref="PhysicalButton"/> (« ClickPlus »), ce qui garde les anciens config.json lisibles.
/// Clavier : « kbd:F13 ». Manette Xbox : « pad:A ». Joystick générique : « joy:VID_PID:B3 ».
/// </summary>
[JsonConverter(typeof(ButtonKeyJsonConverter))]
public readonly record struct ButtonKey(string Id)
{
    public const string KeyboardPrefix = "kbd:";
    public const string PadPrefix = "pad:";
    public const string JoystickPrefix = "joy:";
    public const string ControllerPrefix = "ctl:";

    public static ButtonKey Keyboard(string name) => new(KeyboardPrefix + name);
    public static ButtonKey Pad(string name) => new(PadPrefix + name);
    public static ButtonKey Joystick(string deviceId, string name) => new($"{JoystickPrefix}{deviceId}:{name}");

    /// <summary>Touche d'une manette BLE non Zwift : « ctl:vs200:Up » (modèle, puis nom de la touche).</summary>
    public static ButtonKey Controller(ControllerKind kind, string name) =>
        new($"{ControllerPrefix}{kind.Slug()}:{name}");

    public bool IsController => Id.StartsWith(ControllerPrefix, StringComparison.Ordinal);

    /// <summary>Découpe « ctl:modèle:touche » ; faux si la clé n'est pas une touche de manette connue.</summary>
    public bool TryAsController(out ControllerKind kind, out string name)
    {
        kind = default; name = "";
        if (!IsController) return false;
        var rest = Id[ControllerPrefix.Length..];
        int i = rest.IndexOf(':');
        if (i <= 0 || !ControllerKinds.TryFromSlug(rest[..i], out kind)) return false;
        name = rest[(i + 1)..];
        return true;
    }

    public bool IsKeyboard => Id.StartsWith(KeyboardPrefix, StringComparison.Ordinal);
    public bool IsPad => Id.StartsWith(PadPrefix, StringComparison.Ordinal);
    public bool IsJoystick => Id.StartsWith(JoystickPrefix, StringComparison.Ordinal);

    /// <summary>Vrai pour une manette Zwift (identité = nom d'enum <see cref="PhysicalButton"/>).</summary>
    public bool IsZwift => TryAsPhysical(out _);

    /// <summary>Vrai pour toute source locale (clavier, manette de jeu, joystick) par opposition aux manettes Zwift.</summary>
    public bool IsLocalInput => IsKeyboard || IsPad || IsJoystick;

    public bool TryAsPhysical(out PhysicalButton button) =>
        Enum.TryParse(Id, out button) && Enum.IsDefined(button) && !char.IsDigit(Id[0]);

    public static implicit operator ButtonKey(PhysicalButton b) => new(b.ToString());

    public override string ToString() => Id;
}

/// <summary>Sérialise <see cref="ButtonKey"/> en simple chaîne, y compris comme clé de dictionnaire.</summary>
public sealed class ButtonKeyJsonConverter : JsonConverter<ButtonKey>
{
    public override ButtonKey Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? "");

    public override void Write(Utf8JsonWriter writer, ButtonKey value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.Id);

    public override ButtonKey ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        new(reader.GetString() ?? "");

    public override void WriteAsPropertyName(Utf8JsonWriter writer, ButtonKey value, JsonSerializerOptions options) =>
        writer.WritePropertyName(value.Id);
}
