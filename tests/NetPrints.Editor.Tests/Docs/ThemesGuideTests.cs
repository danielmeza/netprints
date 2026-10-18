using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Architecture;
using NetPrints.Editor.Theming;

namespace NetPrints.Editor.Tests.Docs;

/// <summary>The token list of the theming guide is the one the catalogue generates (FR-066, FR-116).</summary>
public sealed class ThemesGuideTests
{
    private const string Begin = "<!-- tokens:begin -->";
    private const string End = "<!-- tokens:end -->";

    private static string GuidePath => Path.Combine(RepositoryPaths.Root(), "docs", "guide", "editor", "themes.md");

    private static string Generate() =>
        string.Join('\n', ["| Token | Kind | One value per variant |", "|---|---|---|", .. ThemeTokenCatalogue.All.Select(token => $"| `{token.Name}` | {token.Kind} | {(token.PerVariant ? "yes" : "no")} |")]);

    private static string TableOf(string guide)
    {
        int start = guide.IndexOf(Begin, StringComparison.Ordinal) + Begin.Length;
        int end = guide.IndexOf(End, StringComparison.Ordinal);
        return guide[start..end].Trim();
    }

    [Fact]
    public void TheTokenListInTheGuideIsTheOneGeneratedFromTheCatalogue()
    {
        string guide = File.ReadAllText(GuidePath).ReplaceLineEndings("\n");
        if (Environment.GetEnvironmentVariable(ShortcutGuideTests.UpdateVariable) == "1")
        {
            int start = guide.IndexOf(Begin, StringComparison.Ordinal) + Begin.Length;
            int end = guide.IndexOf(End, StringComparison.Ordinal);
            guide = guide[..start] + "\n" + Generate() + "\n" + guide[end..];
            File.WriteAllText(GuidePath, guide);
        }

        Assert.Equal(Generate(), TableOf(guide));
    }

    [Fact]
    public void TheGuideNamesEveryBuiltInPresetAndTheDefaults()
    {
        string guide = File.ReadAllText(GuidePath);

        Assert.All(ThemePresets.BuiltIn, preset => Assert.Contains($"`{preset.Id}`", guide, StringComparison.Ordinal));
        Assert.Contains($"`{EditorSettings.DefaultDarkPreset}`", guide, StringComparison.Ordinal);
        Assert.Contains("darkPreset", guide, StringComparison.Ordinal);
        Assert.Contains("lightPreset", guide, StringComparison.Ordinal);
    }
}
