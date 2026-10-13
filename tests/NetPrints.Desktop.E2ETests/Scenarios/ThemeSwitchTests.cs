using System.Text.Json;
using System.Xml.Linq;
using Avalonia.Media;
using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Icons;
using NetPrints.Testing;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Shell;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>View › Theme › Light restyles the canvas, a pane, a node header, the command bar and an empty state, and the choice survives a restart (FR-082, US9).</summary>
public sealed class ThemeSwitchTests(DesktopWorkerPool pool) : ProjectEditorTestBase(pool)
{
    private const string MainMethod = "Main";
    private const string CallNode = "CallMethodNode";
    private const string DarkVariant = "Dark";
    private const string LightVariant = "Light";
    private const string StyleTokenCanvas = "Canvas.Background";
    private const string StyleTokenCallHeader = "Node.Header.Call";
    private const string StoredLight = "light";
    private const int ChannelTolerance = 3;
    private const int LuminanceMidpoint = 128;
    private const int PanePixelInset = 6;

    private string ConfigDirectory => Path.Combine(Work, "config");

    private string? SettingsFile => Directory.EnumerateFiles(Work, "settings.json", SearchOption.AllDirectories).FirstOrDefault();

    protected override EditorStart EditorStartFor(string sampleProject) => new(sampleProject, IsolatedEnvironment(), WaitForProject: true);

    private Dictionary<string, string> IsolatedEnvironment()
    {
        Dictionary<string, string> variables = Environment();
        variables["XDG_CONFIG_HOME"] = ConfigDirectory;
        return variables;
    }

    private static string StyleToken(string variant, string key)
    {
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        XDocument styles = XDocument.Load(Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "src", "NetPrints.Editor", "EditorStyles.axaml"));
        XElement dictionary = styles.Descendants().First(e => (string?)e.Attribute(x + "Key") == variant && e.Parent?.Name.LocalName.EndsWith("ThemeDictionaries", StringComparison.Ordinal) == true);
        string value = dictionary.Elements().First(e => (string?)e.Attribute(x + "Key") == key).Value.Trim();
        return Color.Parse(value).ToString();
    }

    private string? StoredTheme()
    {
        try
        {
            if (SettingsFile is not { } file)
            {
                return null;
            }

            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(file));
            return document.RootElement.GetProperty("extensions").GetProperty("netprints.editor").GetProperty("theme").GetString();
        }
        catch (Exception e) when (e is IOException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    private static int Luminance(UiImage image, int x, int y)
    {
        uint pixel = image.Pixel(x, y);
        return (int)((((pixel >> 16) & 0xFF) * 299 + ((pixel >> 8) & 0xFF) * 587 + (pixel & 0xFF) * 114) / 1000);
    }

    private static bool Near(uint pixel, string color)
    {
        Color expected = Color.Parse(color);
        return Math.Abs(((pixel >> 16) & 0xFF) - expected.R) <= ChannelTolerance
            && Math.Abs(((pixel >> 8) & 0xFF) - expected.G) <= ChannelTolerance
            && Math.Abs((pixel & 0xFF) - expected.B) <= ChannelTolerance;
    }

    private async Task<string?> CanvasBackgroundAsync(CancellationToken cancellationToken) => (await Shell.Graph.GridColorsAsync(cancellationToken)).Background;

    private async Task<int> PaneLuminanceAsync(CancellationToken cancellationToken)
    {
        UiImage pane = await Shell.PanelContent(PanelContributions.ProjectTreeId).ScreenshotAsync(cancellationToken);
        return Luminance(pane, pane.Width - PanePixelInset, pane.Height - PanePixelInset);
    }

    [Fact]
    public Task LightRestylesTheShellAndSurvivesARestart() => RunScenarioAsync(async token =>
    {
        string darkCanvas = StyleToken(DarkVariant, StyleTokenCanvas);
        string lightCanvas = StyleToken(LightVariant, StyleTokenCanvas);
        string lightCallHeader = StyleToken(LightVariant, StyleTokenCallHeader);
        await StartAsync(token);
        await WaitForProjectAsync(token);
        await Shell.OpenMethodAsync(MainMethod, token);
        int darkPane;

        using (Step("the editor starts Dark"))
        {
            Assert.Equal(darkCanvas, await CanvasBackgroundAsync(token), ignoreCase: true);
            darkPane = await PaneLuminanceAsync(token);
            Assert.True(darkPane < LuminanceMidpoint, $"The pane is dark (luminance {darkPane}).");
        }

        using (Step("View, Theme, Light"))
        {
            await Shell.Menu.InvokeAsync("View", ShellCommands.ThemeLight, token, scrollOverCommandId: ShellCommands.ResetLayout);
            await UiWait.UntilAsync(Driver, async () => string.Equals(await CanvasBackgroundAsync(token), lightCanvas, StringComparison.OrdinalIgnoreCase), $"the canvas {lightCanvas}", token);
            await UiWait.UntilAsync(Driver, async () => await PaneLuminanceAsync(token) >= LuminanceMidpoint, "the pane light", token);
        }

        using (Step("a Call node header, the command bar icons and the empty Errors panel are Light"))
        {
            UiImage header = await Shell.Graph.Node(CallNode).Label.ScreenshotAsync(token);
            Assert.True(Near(header.Pixel(1, 1), lightCallHeader), $"The Call header is {lightCallHeader}, got #{header.Pixel(1, 1):X8}.");

            IReadOnlyList<string?> icons = await Shell.Commands.IconIdsAsync(token);
            Assert.NotEmpty(icons);
            Assert.All(icons, id => Assert.True(id is not null && IconRegistry.Shared.IsKnown(id), $"The command bar draws the fallback glyph for '{id}'."));

            await Shell.Bottom.ShowAsync(PanelContributions.ErrorsId, token);
            await Shell.Bottom.ErrorsClean.WaitVisibleAsync(token);
        }

        using (Step("the choice is stored"))
        {
            await WaitForAsync(() => StoredTheme() == StoredLight, "the theme stored in settings.json", token);
        }

        using (Step("restart"))
        {
            await CloseWindowAndWaitForExitAsync(token);
            await RestartEditorAsync(new EditorStart(Path.Combine(SampleDirectory, "HelloWorld.csproj"), IsolatedEnvironment(), WaitForProject: true), token);
            await WaitForProjectAsync(token);
            await Shell.OpenMethodAsync(MainMethod, token);
            Assert.Equal(lightCanvas, await CanvasBackgroundAsync(token), ignoreCase: true);
            Assert.True(await PaneLuminanceAsync(token) >= LuminanceMidpoint, "The pane is light after the restart.");
        }
    });
}
