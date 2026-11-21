using System.Globalization;
using System.Runtime.CompilerServices;

namespace ShiftBridge.Core.Tests;

/// <summary>
/// Les assertions existantes portent sur les messages français : on fixe la culture d'interface à « fr » pour toute la suite,
/// quelle que soit la langue de la machine qui exécute les tests. Les tests de localisation changent la culture localement.
/// </summary>
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void ForceFrench()
    {
        var fr = CultureInfo.GetCultureInfo("fr");
        CultureInfo.DefaultThreadCurrentUICulture = fr;
        CultureInfo.CurrentUICulture = fr;
    }
}
