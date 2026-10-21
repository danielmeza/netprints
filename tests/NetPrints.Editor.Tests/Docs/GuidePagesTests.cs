using System.Text.RegularExpressions;
using NetPrints.Editor.Tests.Architecture;

namespace NetPrints.Editor.Tests.Docs;

/// <summary>The editor guides exist and are illustrated with screenshots that are in the site's images folder (SC-010).</summary>
public sealed partial class GuidePagesTests
{
    private static readonly string[] Pages =
    [
        "shell.md",
        "start-page.md",
        "saving-and-recovery.md",
        "commands-and-shortcuts.md",
        "navigation.md",
        "event-graphs.md",
    ];

    private static readonly string[] TextOnlyPages = ["settings.md", "themes.md"];

    private static string GuideFolder => Path.Combine(RepositoryPaths.Root(), "docs", "guide", "editor");

    private static string ImagesFolder => Path.Combine(RepositoryPaths.Root(), "website", "static", "img", "guide", "editor");

    public static TheoryData<string> GuidePages() => [.. Pages];

    [GeneratedRegex(@"!\[[^\]]*\]\((?<path>[^)\s]+\.png)\)")]
    private static partial Regex ImageReference();

    private static string[] ImagesOf(string page) =>
        [.. ImageReference().Matches(File.ReadAllText(Path.Combine(GuideFolder, page))).Select(match => match.Groups["path"].Value)];

    [Theory]
    [MemberData(nameof(GuidePages))]
    public void AGuidePageExistsAndReferencesAtLeastOneScreenshot(string page)
    {
        Assert.True(File.Exists(Path.Combine(GuideFolder, page)), $"The guide {page} does not exist.");

        Assert.NotEmpty(ImagesOf(page));
    }

    [Theory]
    [MemberData(nameof(GuidePages))]
    public void EveryScreenshotOfAGuideIsInTheImagesFolder(string page)
    {
        foreach (string reference in ImagesOf(page))
        {
            string file = Path.GetFileName(reference);

            Assert.True(File.Exists(Path.Combine(ImagesFolder, file)), $"{page} refers to {file}, which is not in website/static/img/guide/editor.");
            Assert.EndsWith("website/static/img/guide/editor/" + file, reference, StringComparison.Ordinal);
        }
    }

    [GeneratedRegex("""const string \w+ = "(?<code>NPK\d{3})";""")]
    private static partial Regex CheckCode();

    [Fact]
    public void TheExtensionTestingGuideHasASectionPerCheckAndIsLinkedFromTheIndexAndTheExtensionsGuide()
    {
        string root = RepositoryPaths.Root();
        string guide = File.ReadAllText(Path.Combine(root, "docs", "guide", "extension-testing.md"));
        string codes = File.ReadAllText(Path.Combine(root, "src", "NetPrints.Extensibility.Testing", "NpkDiagnosticCodes.cs"));

        string[] checks = [.. CheckCode().Matches(codes).Select(match => match.Groups["code"].Value)];

        Assert.NotEmpty(checks);
        foreach (string code in checks)
        {
            Assert.Contains("### " + code, guide, StringComparison.Ordinal);
        }

        Assert.Contains("guide/extension-testing.md", File.ReadAllText(Path.Combine(root, "README.md")), StringComparison.Ordinal);
        Assert.Contains("extension-testing.md", File.ReadAllText(Path.Combine(root, "docs", "guide", "extensions.md")), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryGuideOfTheFolderIsListedHere()
    {
        string[] pages = [.. Directory.GetFiles(GuideFolder, "*.md").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];

        Assert.Equal([.. Pages.Concat(TextOnlyPages).Order(StringComparer.Ordinal)], pages);
    }
}
