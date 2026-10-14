using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using NetPrints.Core;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Graph;
using NetPrints.Testing.Ui.Dialogs;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Editor.UITests.Snapshots;

/// <summary>
/// High-DPI snapshots (FR-089): the shell, the inspector, the canvas with every node kind and the Unsaved changes dialog at
/// render scaling 1.0, 1.5 and 2.0 in Dark and Light, as <c>&lt;name&gt;-&lt;dark|light&gt;-&lt;scale&gt;.png</c> baselines. At 2.0 no
/// <see cref="Image"/> shows a <see cref="Bitmap"/>, so every icon stays vector.
/// </summary>
public class ScaledSnapshotTests
{
    private const double TopScale = 2.0;
    private static readonly double[] Scales = [1.0, 1.5, TopScale];
    private static readonly (string Name, ThemeVariant Variant)[] Themes = [("dark", ThemeVariant.Dark), ("light", ThemeVariant.Light)];

    private static readonly UnsavedFile[] Files =
    [
        new("Program.netpc.json", UnsavedFileKind.Class, "Program"),
        new("Other.netpc.json", UnsavedFileKind.Class, "Other"),
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string BaselineName(string name, string theme, double scale) =>
        $"{name}-{theme}-{scale.ToString("0.0", CultureInfo.InvariantCulture)}";

    private static Application CurrentApplication => Application.Current ?? throw new InvalidOperationException("No application.");

    /// <summary>Runs <paramref name="check"/> for each theme and scale of <paramref name="window"/>, then puts back Dark at 1.0.</summary>
    private static async Task ForEachVariantAsync(Window window, Func<string, double, Task> check)
    {
        try
        {
            foreach ((string theme, ThemeVariant variant) in Themes)
            {
                foreach (double scale in Scales)
                {
                    CurrentApplication.RequestedThemeVariant = variant;
                    window.SetRenderScaling(scale);
                    HeadlessDriver.Pump();
                    await check(theme, scale);
                }
            }
        }
        finally
        {
            CurrentApplication.RequestedThemeVariant = ThemeVariant.Dark;
            window.SetRenderScaling(1.0);
            HeadlessDriver.Pump();
        }
    }

    private static async Task<UiImage> CropAsync(IUiDriver driver, UiElement element, double scale)
    {
        var info = await element.GetAsync(Token);
        var frame = await driver.ScreenshotAsync(info.Window, Token);
        return frame.Crop((int)Math.Round(info.Bounds.X * scale), (int)Math.Round(info.Bounds.Y * scale),
            (int)Math.Round(info.Bounds.Width * scale), (int)Math.Round(info.Bounds.Height * scale));
    }

    private static async Task<SnapshotMask> MaskOfAsync(UiElement element, double scale)
    {
        var bounds = (await element.GetAsync(Token)).Bounds;
        return new SnapshotMask((int)((bounds.X - 2) * scale), (int)((bounds.Y - 2) * scale), (int)((bounds.Width + 4) * scale), (int)((bounds.Height + 4) * scale));
    }

    private static Task MatchAsync(string name, Func<CancellationToken, Task<UiImage>> capture, SnapshotOptions? options = null) =>
        UiArtifacts.Snapshots.MatchStableAsync(name, capture, options, cancellationToken: Token);

    private static void AssertNoBitmapImages(Window window) =>
        Assert.Empty(window.GetVisualDescendants().OfType<Image>().Where(image => image.Source is Bitmap).Select(image => image.Name ?? image.GetType().Name));

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheShellTheInspectorAndTheCanvasAtEveryScaleAndTheme()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await session.WaitStatusIdleAsync(Token);
        var window = session.Window;
        var status = new UiElement(session.Driver, new AutomationQuery(AutomationIds.ShellStatusBar)); // "Loaded project" fades after 5 s

        await ForEachVariantAsync(window, async (theme, scale) =>
        {
            var mask = await MaskOfAsync(status, scale);
            await MatchAsync(BaselineName("editor-shell-main", theme, scale), async _ => await CropAsync(session.Driver, session.Page, scale), new SnapshotOptions { Masks = [mask] });
            await MatchAsync(BaselineName("inspector-method", theme, scale), async _ => await CropAsync(session.Driver, session.Page.Inspector, scale));
            if (scale == TopScale)
            {
                AssertNoBitmapImages(window);
            }
        });

        await session.AddVariableAsync(Token);
        var variable = session.ClassContext.Variables.Single().Variable.Specifier;
        var graph = session.GraphViewModel;
        Func<GraphPoint, Node>[] add =
        [
            p => graph.AddNode<IfElseNode>(p),
            p => graph.AddNode<ForLoopNode>(p),
            p => graph.AddNode<LiteralNode>(p, null, TypeSpecifier.FromType<int>()),
            p => graph.AddNode<TernaryNode>(p),
            p => graph.AddNode<ThrowNode>(p),
            p => graph.AddNode<MakeArrayNode>(p),
            p => graph.AddNode<TypeNode>(p, null, TypeSpecifier.FromType<string>()),
            p => graph.AddNode<ExplicitCastNode>(p),
            p => graph.AddNode<VariableGetterNode>(p, null, variable),
            p => graph.AddNode<VariableSetterNode>(p, null, variable),
            p => graph.AddNode<DefaultNode>(p),
            p => graph.AddNode<AwaitNode>(p),
        ];
        for (int i = 0; i < add.Length; i++)
        {
            add[i](new GraphPoint(28 + i % 4 * 336, 280 + i / 4 * 224));
        }

        await session.WaitForRenderedAsync(Token);
        for (int i = 0; i < 3; i++)
        {
            await session.Graph.WheelAsync(await session.Graph.OffsetAsync(0, 0, Token), -1, Token);
        }

        await ForEachVariantAsync(window, async (theme, scale) =>
            await MatchAsync(BaselineName("canvas-every-node-kind", theme, scale), async _ => await CropAsync(session.Driver, session.Graph, scale)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheUnsavedChangesDialogAtEveryScaleAndTheme()
    {
        using var ui = HeadlessUi.Create();
        var dialog = ui.Show(new UnsavedChangesDialog(Files));
        var page = new UnsavedChangesDialogPage(ui.Driver);

        await ForEachVariantAsync(dialog, async (theme, scale) =>
        {
            await MatchAsync(BaselineName("dialog-unsaved-changes", theme, scale), async _ =>
                await ui.Driver.ScreenshotAsync((await page.GetAsync(Token)).Window, Token));
            if (scale == TopScale)
            {
                AssertNoBitmapImages(dialog);
            }
        });
    }
}
