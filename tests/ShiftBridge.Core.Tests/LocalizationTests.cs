using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ShiftBridge.Core.Input;
using ShiftBridge.Core.Localization;
using ShiftBridge.Core.Obc;
using ShiftBridge.Presentation;

namespace ShiftBridge.Core.Tests;

/// <summary>
/// Localisation : cohérence des fichiers de ressources (anglais neutre / français), choix de la langue et rendu des textes
/// dans chaque langue. Aucun test ne modifie la culture par défaut du processus (les autres tests supposent le français) : la
/// culture n'est changée que sur le thread du test, et restaurée.
/// </summary>
public class LocalizationTests
{
    private static readonly CultureInfo En = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr");

    /// <summary>Exécute <paramref name="body"/> avec la culture d'interface donnée sur ce thread, puis la restaure.</summary>
    private static void Under(CultureInfo culture, Action body)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = culture;
        try { body(); }
        finally { CultureInfo.CurrentUICulture = previous; }
    }

    private static string LocalizationDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", "ShiftBridge.Core", "Localization");
            if (File.Exists(Path.Combine(candidate, "Strings.resx"))) return candidate;
        }
        throw new DirectoryNotFoundException("src/ShiftBridge.Core/Localization introuvable depuis " + AppContext.BaseDirectory);
    }

    private static Dictionary<string, string> LoadResx(string fileName) =>
        XDocument.Load(Path.Combine(LocalizationDir(), fileName)).Root!.Elements("data")
            .ToDictionary(d => (string)d.Attribute("name")!, d => (string?)d.Element("value") ?? "");

    private static SortedSet<int> Placeholders(string text) =>
        [.. Regex.Matches(text, @"\{(\d+)(?:[:,][^}]*)?\}").Select(m => int.Parse(m.Groups[1].Value))];

    // ---------- fichiers de ressources ----------

    [Fact]
    public void English_and_French_resources_have_exactly_the_same_keys()
    {
        var en = LoadResx("Strings.resx");
        var fr = LoadResx("Strings.fr.resx");
        Assert.Empty(en.Keys.Except(fr.Keys));
        Assert.Empty(fr.Keys.Except(en.Keys));
        Assert.True(en.Count > 300, $"seulement {en.Count} clés : le fichier de ressources semble tronqué");
    }

    [Fact]
    public void No_resource_value_is_empty()
    {
        foreach (var (file, values) in new[] { ("Strings.resx", LoadResx("Strings.resx")), ("Strings.fr.resx", LoadResx("Strings.fr.resx")) })
            Assert.Empty(values.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => $"{file}:{kv.Key}"));
    }

    [Fact]
    public void Both_languages_use_the_same_format_placeholders()
    {
        var en = LoadResx("Strings.resx");
        var fr = LoadResx("Strings.fr.resx");
        var mismatched = en.Keys.Where(k => fr.ContainsKey(k) && !Placeholders(en[k]).SetEquals(Placeholders(fr[k]))).ToList();
        Assert.True(mismatched.Count == 0, "Paramètres {n} différents entre en et fr : " + string.Join(", ", mismatched));
    }

    [Fact]
    public void Every_resource_string_is_a_valid_format_string_in_both_languages()
    {
        // Une accolade égarée ou un spécificateur invalide ne planterait qu'à l'affichage du message concerné.
        // Tous les formats utilisés sont numériques (0.00, P0, X2, +0.0%;-0.0%…) : des entiers conviennent comme arguments.
        object[] args = [5, 6, 7, 8, 9, 10];
        var failures = new List<string>();
        foreach (var (file, values) in new[] { ("en", LoadResx("Strings.resx")), ("fr", LoadResx("Strings.fr.resx")) })
            foreach (var (key, text) in values)
            {
                try { _ = string.Format(CultureInfo.InvariantCulture, text, args); }
                catch (FormatException ex) { failures.Add($"{file}:{key} ({ex.Message})"); }
            }
        Assert.True(failures.Count == 0, string.Join("; ", failures));
    }

    [Fact]
    public void English_texts_do_not_contain_accented_french_leftovers()
    {
        // Les noms propres (« Français ») ne sont pas dans les ressources ; un accent français dans le texte anglais trahit un oubli.
        var leaks = LoadResx("Strings.resx")
            .Where(kv => Regex.IsMatch(kv.Value, "[éèêàùôçÉ«»]"))
            .Select(kv => kv.Key).ToList();
        Assert.True(leaks.Count == 0, "Texte anglais contenant du français : " + string.Join(", ", leaks));
    }

    [Fact]
    public void Resource_manager_falls_back_french_regions_to_french_and_others_to_english()
    {
        var rm = Strings.ResourceManager;
        Assert.Equal("Aucune manette", rm.GetString("Ctrl_None", CultureInfo.GetCultureInfo("fr-BE")));
        Assert.Equal("Aucune manette", rm.GetString("Ctrl_None", CultureInfo.GetCultureInfo("fr-CA")));
        Assert.Equal("No controller", rm.GetString("Ctrl_None", CultureInfo.GetCultureInfo("de-DE")));
        Assert.Equal("No controller", rm.GetString("Ctrl_None", CultureInfo.InvariantCulture));
    }

    // ---------- choix de la langue ----------

    [Theory]
    [InlineData(null, "fr-FR", "fr")]
    [InlineData(null, "fr-BE", "fr")]
    [InlineData(null, "fr-CH", "fr")]
    [InlineData(null, "fr-CA", "fr")]
    [InlineData("auto", "fr-BE", "fr")]
    [InlineData(null, "en-US", "en")]
    [InlineData(null, "de-DE", "en")]
    [InlineData(null, "nl-BE", "en")]      // néerlandophone belge : anglais, pas français
    [InlineData("en", "fr-BE", "en")]      // forcé en anglais sur un système francophone
    [InlineData("fr", "en-US", "fr")]      // forcé en français sur un système anglophone
    [InlineData(" EN ", "fr-FR", "en")]    // tolère espaces et casse (fichier édité à la main)
    [InlineData("xx", "fr-FR", "fr")]      // valeur inconnue : retombe sur la détection automatique
    [InlineData("xx", "de-DE", "en")]
    public void Language_is_resolved_from_preference_and_system_language(string? preference, string system, string expected) =>
        Assert.Equal(expected, AppLanguage.Resolve(preference, CultureInfo.GetCultureInfo(system)));

    [Theory]
    [InlineData(null, "fr-BE", "fr-BE")]   // la région francophone est conservée
    [InlineData("fr", "fr-CH", "fr-CH")]
    [InlineData("fr", "de-DE", "fr")]
    [InlineData(null, "de-DE", "en")]
    [InlineData("en", "fr-FR", "en")]
    public void Chosen_culture_keeps_a_french_region_and_is_otherwise_fr_or_en(string? preference, string system, string expected) =>
        Assert.Equal(expected, AppLanguage.Choose(preference, CultureInfo.GetCultureInfo(system)).Name);

    [Theory]
    [InlineData(null, "auto")]
    [InlineData("", "auto")]
    [InlineData("auto", "auto")]
    [InlineData("FR", "fr")]
    [InlineData("en ", "en")]
    [InlineData("zz", "auto")]
    public void Preference_is_normalised(string? preference, string expected) =>
        Assert.Equal(expected, AppLanguage.Normalize(preference));

    [Fact]
    public void Language_preference_is_saved_in_the_config_and_defaults_to_automatic()
    {
        Assert.Null(new Config.AppConfig().Language);
        var path = Path.Combine(Path.GetTempPath(), "shiftbridge-lang-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var config = new Config.AppConfig { Language = "en" };
            Config.ConfigStore.Save(config, path);
            Assert.Equal("en", Config.ConfigStore.Load(path).Language);
        }
        finally { File.Delete(path); }
    }

    // ---------- rendu dans chaque langue ----------

    [Fact]
    public void Action_catalog_is_translated_in_both_languages_with_no_missing_key()
    {
        foreach (var culture in new[] { En, Fr })
            Under(culture, () =>
            {
                foreach (var (id, category, label) in ObcButtonCatalog.Assignable)
                {
                    Assert.False(label.StartsWith("Action_"), $"{culture.Name}: libellé manquant pour {id}");
                    Assert.False(category.StartsWith("Cat_"), $"{culture.Name}: catégorie manquante pour {id}");
                }
            });

        Under(En, () =>
        {
            var up = ObcButtonCatalog.Assignable.Single(a => a.Id == ObcButtonId.ShiftUp);
            Assert.Equal(("Gears", "Shift up"), (up.Category, up.Label));
        });
        Under(Fr, () =>
        {
            var up = ObcButtonCatalog.Assignable.Single(a => a.Id == ObcButtonId.ShiftUp);
            Assert.Equal(("Vitesses", "Vitesse supérieure"), (up.Category, up.Label));
        });
    }

    [Fact]
    public void Key_names_follow_the_ui_language()
    {
        Under(En, () =>
        {
            Assert.Equal("Keyboard: Next track", ButtonKeyNames.Label(ButtonKey.Keyboard("MediaNextTrack")));
            Assert.Equal("Controller: Left trigger (LT)", ButtonKeyNames.Label(ButtonKey.Pad("LT")));
            Assert.Equal("Joystick 046D_C21D: Button 3", ButtonKeyNames.Label(ButtonKey.Joystick("046D_C21D", "B3")));
            Assert.Equal("Joystick 046D_C21D: Axis X +", ButtonKeyNames.Label(ButtonKey.Joystick("046D_C21D", "XPlus")));
            Assert.Equal("Pro Controller: D-pad: up", ButtonKeyNames.Label(ButtonKey.Joystick("057E_2009", "DPadUp")));
            Assert.Equal("Joy-Con (L): +", ButtonKeyNames.Label(ButtonKey.Joystick("057E_2006", "Plus")));
            Assert.Equal("F13", ButtonKeyNames.Short(ButtonKey.Keyboard("F13"))); // sans libellé propre : nom de la touche
        });
        Under(Fr, () =>
        {
            Assert.Equal("Clavier : Piste suivante", ButtonKeyNames.Label(ButtonKey.Keyboard("MediaNextTrack")));
            Assert.Equal("Joy-Con (G) : +", ButtonKeyNames.Label(ButtonKey.Joystick("057E_2006", "Plus")));
        });
    }

    [Fact]
    public void Every_named_keyboard_and_pad_key_has_a_resource_in_both_languages()
    {
        // Une clé Kbd_* / Pad_* absente du jeu de noms du code s'afficherait sous son nom brut : on vérifie les deux sens.
        foreach (var (name, value) in LoadResx("Strings.resx").Where(kv => kv.Key.StartsWith("Kbd_")).Select(kv => (kv.Key[4..], kv.Value)))
            Under(En, () => Assert.Equal(value, ButtonKeyNames.Short(ButtonKey.Keyboard(name))));
        foreach (var (name, value) in LoadResx("Strings.resx").Where(kv => kv.Key.StartsWith("Pad_")).Select(kv => (kv.Key[4..], kv.Value)))
            Under(En, () => Assert.Equal(value, ButtonKeyNames.Short(ButtonKey.Pad(name))));
        foreach (var (name, value) in LoadResx("Strings.fr.resx").Where(kv => kv.Key.StartsWith("Kbd_")).Select(kv => (kv.Key[4..], kv.Value)))
            Under(Fr, () => Assert.Equal(value, ButtonKeyNames.Short(ButtonKey.Keyboard(name))));
    }

    [Fact]
    public void Plurals_pick_the_singular_or_the_plural_form()
    {
        Under(En, () =>
        {
            Assert.Equal("1 controller connected", Loc.Plural(1, Strings.Ctrl_Connected_One, Strings.Ctrl_Connected_Many));
            Assert.Equal("3 controllers connected", Loc.Plural(3, Strings.Ctrl_Connected_One, Strings.Ctrl_Connected_Many));
            Assert.Equal("1 Bluetooth trainer found. Select yours.", Loc.Plural(1, Strings.Ble_Found_One, Strings.Ble_Found_Many));
            Assert.Equal("2 Bluetooth trainers found. Select yours.", Loc.Plural(2, Strings.Ble_Found_One, Strings.Ble_Found_Many));
        });
        Under(Fr, () =>
        {
            Assert.Equal("1 manette connectée", Loc.Plural(1, Strings.Ctrl_Connected_One, Strings.Ctrl_Connected_Many));
            Assert.Equal("3 manettes connectées", Loc.Plural(3, Strings.Ctrl_Connected_One, Strings.Ctrl_Connected_Many));
        });
    }

    [Fact]
    public void Numbers_in_messages_follow_the_regional_format_not_the_ui_language()
    {
        // Texte en anglais sur un système allemand : « 2,43 » comme partout ailleurs dans l'application (jamais un mélange).
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            Under(En, () => Assert.Equal("Real ratio 2,43 (34×14).", Loc.Fmt(Strings.Drive_RealRatio, 2.4286, "34×14")));
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
            Under(Fr, () => Assert.Equal("Rapport réel 2.43 (34×14).", Loc.Fmt(Strings.Drive_RealRatio, 2.4286, "34×14")));
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void Local_network_summary_is_translated()
    {
        Under(En, () => Assert.Equal(
            "Addresses: 192.168.1.20 (from another device) · 127.0.0.1 (this device). Ports: OBC network 36867, DirCon 36866.",
            LocalNetwork.Describe(["192.168.1.20"], (Strings.Net_ObcNetwork, 36867), ("DirCon", 36866))));
        Under(Fr, () => Assert.Equal(
            "Adresses : 192.168.1.20 (depuis un autre appareil) · 127.0.0.1 (cet appareil). Ports : OBC réseau 36867, DirCon 36866.",
            LocalNetwork.Describe(["192.168.1.20"], (Strings.Net_ObcNetwork, 36867), ("DirCon", 36866))));
    }

    [Fact]
    public void Unknown_key_falls_back_to_the_key_itself()
    {
        Assert.Equal("No_Such_Key", Loc.Get("No_Such_Key"));
    }
}
