namespace ShiftBridge.Core.Input;

/// <summary>
/// Boutons physiques, indépendants du modèle de manette. Les noms suivent la disposition Zwift :
/// pour Play et Ride, « Left »/« Right » désigne la manette (gauche ou droite du guidon).
/// </summary>
public enum PhysicalButton
{
    // Click (v1)
    ClickPlus,
    ClickMinus,

    // Manette droite (Play, Ride, Click v2)
    RightShiftUp,
    RightShiftDown,
    RightA,
    RightB,
    RightY,
    RightZ,
    RightPowerUp,
    RightOnOff,
    RightPaddle,

    // Manette gauche
    LeftShiftUp,
    LeftShiftDown,
    LeftUp,
    LeftDown,
    LeftLeft,
    LeftRight,
    LeftPowerUp,
    LeftOnOff,
    LeftPaddle,
}
