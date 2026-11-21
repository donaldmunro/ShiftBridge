using System.Text.RegularExpressions;
using ShiftBridge.Presentation;
using Xunit;

namespace ShiftBridge.Core.Tests;

/// <summary>Hiérarchie des pages (bouton retour) et cohérence des clés de page citées dans les XAML Windows et Android.</summary>
public class NavigationTests
{
    [Theory]
    [InlineData(Pages.Diagnostic, Pages.Settings)]
    [InlineData(Pages.Settings, Pages.Home)]
    [InlineData(Pages.Devices, Pages.Home)]
    [InlineData(Pages.Buttons, Pages.Home)]
    [InlineData(Pages.Trainer, Pages.Home)]
    [InlineData(Pages.Apps, Pages.Home)]
    public void Parent_of_a_page(string page, string expected) => Assert.Equal(expected, Pages.ParentOf(page));

    [Fact]
    public void Home_has_no_parent_so_back_exits_the_app() => Assert.Null(Pages.ParentOf(Pages.Home));

    [Fact]
    public void Every_page_reaches_home_in_at_most_two_steps()
    {
        foreach (var page in Pages.All)
        {
            int steps = 0;
            for (string? p = page; p is not null && p != Pages.Home; p = Pages.ParentOf(p))
                Assert.True(++steps <= 2, $"{page} est trop profonde");
        }
    }

    [Fact]
    public void Page_keys_are_unique() => Assert.Equal(Pages.All.Count, Pages.All.Distinct().Count());

    private static string SrcDir()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "ShiftBridge.Presentation"))) return Path.Combine(dir.FullName, "src");
        throw new DirectoryNotFoundException("src introuvable depuis " + AppContext.BaseDirectory);
    }

    /// <summary>Toute page citée par un XAML (NavigateCommand, ConverterParameter de la navigation) doit exister : une faute de frappe ne ferait rien, en silence.</summary>
    [Fact]
    public void Pages_named_in_xaml_exist()
    {
        var known = Pages.All.ToHashSet();
        var files = Directory.EnumerateFiles(SrcDir(), "*.xaml", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.NotEmpty(files);

        // CommandParameter="devices" à côté d'un NavigateCommand ; ConverterParameter=home / 'settings|diagnostic' sur CurrentPage.
        var navigate = new Regex(@"NavigateCommand\}""\s+CommandParameter=""(?<p>[a-z]+)""", RegexOptions.Singleline);
        var current = new Regex(@"Binding CurrentPage[^}]*ConverterParameter='?(?<p>[a-z|]+)'?");
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            foreach (Match m in navigate.Matches(text).Concat(current.Matches(text)))
                foreach (var page in m.Groups["p"].Value.Split('|'))
                    Assert.True(known.Contains(page), $"{Path.GetFileName(file)} cite la page inconnue « {page} »");
        }
    }
}
