using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Commands.KeyboardShortcuts;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Controls;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.Output;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Extensibility.Loading;
using NetPrints.Graph;
using NetPrints.Reflection;
using NetPrints.Testing.Ui.Driving;
using NetPrints.Testing.Ui.Snapshots;

namespace NetPrints.Editor.UITests.Snapshots;

/// <summary>
/// The sub-phase G contact sheet (FR-089): every panel, the start page, a graph, the eight P3a dialogs, Override method and the
/// overloads flyout in Dark and Light at 1.0 and 2.0, then the owner's open calls on the graph tile and the product mark. The sheet
/// is written as <c>contact-sheet.png</c> to the UI artifacts folder; no baseline is compared, a reviewer reads it.
/// </summary>
public class ContactSheetTests
{
    private const int CellWidth = 560;
    private const int CellHeight = 420;
    private const int PanelWidth = 700;
    private const int PanelHeight = 525;
    private const int Rows = 18;
    private const int OwnerCalls = 5;
    private const int MarkTiles = 2;

    private static readonly (string Name, ThemeVariant Variant, double Scale)[] Variants =
    [
        ("Dark 1.0", ThemeVariant.Dark, 1.0),
        ("Light 1.0", ThemeVariant.Light, 1.0),
        ("Dark 2.0", ThemeVariant.Dark, 2.0),
        ("Light 2.0", ThemeVariant.Light, 2.0),
    ];

    private static readonly UnsavedFile[] Files =
    [
        new("Program.netpc.json", UnsavedFileKind.Class, "Program"),
        new("Other.netpc.json", UnsavedFileKind.Class, "Other"),
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static Application CurrentApplication => Application.Current ?? throw new InvalidOperationException("No application.");

    private static ContributionRegistry Registry()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        registry.Freeze();
        return registry;
    }

    internal static IReadOnlyList<CodeDiagnostic> Issues() =>
    [
        new(CodeDiagnosticSeverity.Error, ExtensionDiagnosticCodes.AssemblyLoadFailed, "Could not load the assembly of the extension 'Acme.Nodes'.", null, null, null, null, null),
        new(CodeDiagnosticSeverity.Warning, ExtensionDiagnosticCodes.ApiVersion, "The extension 'Acme.Tools' targets API 2.0; this editor offers 1.0.", null, null, null, null, null),
    ];

    internal static IReadOnlyList<MethodSpecifier> OverrideCandidates(IReflectionProvider provider) =>
        [.. provider.GetOverridableMethodsForType(TypeSpecifier.FromType<Exception>())];

    private static UiImage Frame(Window window)
    {
        HeadlessDriver.Pump();
        using var frame = window.CaptureRenderedFrame() ?? throw new InvalidOperationException("No frame rendered.");
        using var stream = new MemoryStream();
        frame.Save(stream, new PngBitmapEncoderOptions());
        return new UiImage(stream.ToArray());
    }

    /// <summary>The element's pixels; with <paramref name="topLeftOnly"/> only its top-left cell, so a wide panel stays legible.</summary>
    private static async Task<UiImage> CropAsync(IUiDriver driver, UiElement element, double scale, bool topLeftOnly = false)
    {
        var info = await element.GetAsync(Token);
        var frame = await driver.ScreenshotAsync(info.Window, Token);
        double width = topLeftOnly ? Math.Min(info.Bounds.Width, PanelWidth) : info.Bounds.Width;
        double height = topLeftOnly ? Math.Min(info.Bounds.Height, PanelHeight) : info.Bounds.Height;
        return frame.Crop((int)Math.Round(info.Bounds.X * scale), (int)Math.Round(info.Bounds.Y * scale), (int)Math.Round(width * scale), (int)Math.Round(height * scale));
    }

    /// <summary>Shrinks a tile to its size at scale 1.0, then into the cell, so a 2.0 tile is as big as its 1.0 twin.</summary>
    private static UiImage Cell(UiImage image, double scale = 1.0) =>
        image.Fit((int)(image.Width / scale), (int)(image.Height / scale)).Fit(CellWidth, CellHeight);

    private static bool IsFlat(UiImage image)
    {
        uint first = image.Pixel(0, 0);
        for (int y = 0; y < image.Height; y += 3)
        {
            for (int x = 0; x < image.Width; x += 3)
            {
                if (image.Pixel(x, y) != first)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void Apply(ThemeVariant variant, double scale, IEnumerable<Window> windows)
    {
        CurrentApplication.RequestedThemeVariant = variant;
        foreach (Window window in windows)
        {
            window.SetRenderScaling(scale);
        }

        HeadlessDriver.Pump();
    }

    /// <summary>Puts a connected and an unconnected type pin and a coloured data wire on the graph, so the owner's calls have something to act on.</summary>
    private static async Task AddTypedWiresAsync(EditorSession session)
    {
        var graph = session.GraphViewModel;
        var call = graph.Nodes.Single(node => node.Node is CallMethodNode);
        var typeNode = Assert.IsType<TypeNode>(graph.AddNode<TypeNode>(new GraphPoint(28, 280), null, TypeSpecifier.FromType<string>()));
        var ternary = Assert.IsType<TernaryNode>(graph.AddNode<TernaryNode>(new GraphPoint(300, 280)));
        var literal = Assert.IsType<LiteralNode>(graph.AddNode<LiteralNode>(new GraphPoint(28, 520), null, TypeSpecifier.FromType<string>()));
        literal.InputValuePin.UnconnectedValue = "Hello from a literal";
        await session.WaitForRenderedAsync(Token);
        NodePinViewModel PinOf(NodePin pin) => graph.Nodes.SelectMany(node => node.AllPins).Single(p => p.Pin == pin);
        Assert.True(graph.Connect(PinOf(typeNode.OutputTypePins[0]), PinOf(ternary.TypePin)));
        Assert.True(graph.Connect(PinOf(literal.ValuePin), call.InputDataPins.Single()));
        await session.WaitForRenderedAsync(Token);
    }

    /// <summary>The scene of every graph tile: the typed wires, then the viewport fitted to all nodes.</summary>
    private static async Task BuildSceneAsync(EditorSession session)
    {
        await AddTypedWiresAsync(session);
        var before = await session.Graph.ViewportAsync(Token);
        await session.RunAsync("fitAll", Token);
        await UiWait.UntilAsync(session.Driver, async () => await session.Graph.ViewportAsync(Token) != before, "the viewport fitted", Token);
        HeadlessDriver.Pump();
    }

    /// <summary>Shows the C# panel and returns the code generated for the open class; empty when it never translates.</summary>
    private static async Task<string> GeneratedCodeAsync(EditorSession session)
    {
        await session.Page.Bottom.ShowAsync(PanelContributions.CSharpId, Token);
        session.App.Composition.Context.CodeAnalysis.RequestAnalysis(session.App.Session.Project);
        var panel = Assert.IsType<NetPrints.Editor.CodeView.CSharpPanelViewModel>(session.App.Shell.FindPanel(PanelContributions.CSharpId)?.Content);
        try
        {
            await UiWait.UntilAsync(session.Driver, () => Task.FromResult(panel.Current?.Code.Contains("class Program", StringComparison.Ordinal) == true),
                "the generated code", Token, TimeSpan.FromSeconds(10));
        }
        catch (UiWaitTimeoutException)
        {
        }

        return panel.Current?.Code ?? "";
    }

    private static IReadOnlyList<Rect> NodeBounds(EditorSession session)
    {
        var view = session.Window.GetVisualDescendants().OfType<GraphEditorView>().First();
        return [.. view.GetVisualDescendants().OfType<NodeView>().Select(node => new Rect(node.TranslatePoint(default, view) ?? default, node.TranslatePoint(new Point(node.Bounds.Width, node.Bounds.Height), view) ?? default))];
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheSceneShowsEveryNodeWholeAndApart()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await BuildSceneAsync(session);

        var view = session.Window.GetVisualDescendants().OfType<GraphEditorView>().First();
        var bounds = NodeBounds(session);
        Assert.Equal(session.GraphViewModel.Nodes.Count, bounds.Count);
        var canvas = new Rect(view.Bounds.Size);
        Assert.All(bounds, rect => Assert.True(canvas.Contains(rect.TopLeft) && canvas.Contains(rect.BottomRight), $"{rect} is cut by the canvas {canvas}"));
        for (int i = 0; i < bounds.Count; i++)
        {
            for (int j = i + 1; j < bounds.Count; j++)
            {
                Assert.False(bounds[i].Intersects(bounds[j]), $"{bounds[i]} overlaps {bounds[j]}");
            }
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheSceneTranslatesSoTheCSharpTilesShowCode()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await BuildSceneAsync(session);

        string code = await GeneratedCodeAsync(session);

        Assert.Contains("class Program", code, StringComparison.Ordinal);
        Assert.Contains("WriteLine", code, StringComparison.Ordinal);
    }

    [Fact]
    public void TheIssuesTileListsRealDiagnostics()
    {
        var issues = Issues();

        Assert.NotEmpty(issues);
        Assert.All(issues, issue => Assert.StartsWith("NPX", issue.Id, StringComparison.Ordinal));
        Assert.Equal(issues.Count, new IssuesDialogViewModel(issues).Issues.Count);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task TheOverrideTileListsTheRealOverridableMembersOfItsBaseType()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        var provider = session.App.Composition.Context.Reflection.Provider;
        string Signature(MethodSpecifier method) =>
            $"{method.DeclaringType.ShortName}.{method.Name}({string.Join(",", method.Parameters.Select(parameter => parameter.Value))})";
        var real = provider.GetOverridableMethodsForType(TypeSpecifier.FromType<Exception>()).Select(Signature).ToHashSet(StringComparer.Ordinal);

        var candidates = OverrideCandidates(provider);

        Assert.Contains(candidates, method => method.Name == "ToString");
        Assert.All(candidates, method => Assert.Contains(Signature(method), real));
    }

    /// <summary>The canvas and the open overloads flyout, which may reach past the canvas edge.</summary>
    private static async Task<UiImage> FlyoutAsync(EditorSession session, double scale)
    {
        var canvas = await session.Graph.GetAsync(Token);
        var list = session.Window.GetVisualDescendants().OfType<MethodPickerList>().Single();
        Point origin = list.TranslatePoint(default, session.Window) ?? throw new InvalidOperationException("The flyout is not in the window.");
        var area = new Rect(canvas.Bounds.X, canvas.Bounds.Y, canvas.Bounds.Width, canvas.Bounds.Height).Union(new Rect(origin, list.Bounds.Size));
        var frame = await session.Driver.ScreenshotAsync(canvas.Window, Token);
        return frame.Crop((int)Math.Round(area.X * scale), (int)Math.Round(area.Y * scale), (int)Math.Round(area.Width * scale), (int)Math.Round(area.Height * scale));
    }

    private static Window MarkWindow()
    {
        var image = new Image { Width = 256, Height = 256, Margin = new Thickness(32) };
        image.Bind(Image.SourceProperty, image.GetResourceObservable("App.Mark"));
        return new Window { SizeToContent = SizeToContent.WidthAndHeight, CanResize = false, Content = image };
    }

    private static async Task<UiImage> GraphWithAsync(EditorSession session, GraphEditorView view, Action<ResourceDictionary> overrides)
    {
        var resources = new ResourceDictionary();
        overrides(resources);
        view.Resources = resources;
        HeadlessDriver.Pump();
        try
        {
            return Cell(await CropAsync(session.Driver, session.Graph, 1.0));
        }
        finally
        {
            view.Resources = new ResourceDictionary();
            HeadlessDriver.Pump();
        }
    }

    private static IReadOnlyList<ContactSection> Sections(IReadOnlyList<(string Label, UiImage?[] Cells)> rows, IReadOnlyList<ContactTile> strip)
    {
        List<ContactTile> grid = [];
        foreach ((string label, UiImage?[] cells) in rows)
        {
            for (int i = 0; i < Variants.Length; i++)
            {
                grid.Add(new ContactTile($"{label}, {Variants[i].Name}", cells[i] ?? throw new InvalidOperationException($"No tile for {label}, {Variants[i].Name}.")));
            }
        }

        return [new ContactSection("Panels (top-left 700x525, so each shows its empty state or content), documents and dialogs", grid), new ContactSection("The new defaults against Classic Dark (same graph, both Dark 1.0; Classic Dark is a test-only override until P3) and the product mark", strip)];
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task EveryPanelDocumentAndDialogInBothThemesAtBothScalesThenTheOwnerCalls()
    {
        await using var session = await EditorSession.OpenSampleMainAsync(Token);
        await BuildSceneAsync(session);
        var output = Assert.IsType<OutputPanelViewModel>(session.App.Shell.FindPanel(PanelContributions.OutputId)?.Content);
        output.Lines.Add(new OutputLineViewModel(OutputLineKind.Build, "Build started."));
        output.Lines.Add(new OutputLineViewModel(OutputLineKind.Build, "Build succeeded"));
        output.Lines.Add(new OutputLineViewModel(OutputLineKind.Output, "Hello, World!"));
        output.Lines.Add(new OutputLineViewModel(OutputLineKind.Error, "Unhandled exception. System.InvalidOperationException: boom"));
        using var start = StartPageRig.Create(1100, 700, withRecent: true);
        using var dialogUi = HeadlessUi.Create();
        using var markUi = HeadlessUi.Create();
        var registry = Registry();
        (string Name, Func<Window> Create)[] dialogs =
        [
            ("Unsaved changes", () => new UnsavedChangesDialog(Files)),
            ("Confirm", () => new ConfirmDialog("Delete it", "This cannot be undone.", "Delete")),
            ("Keyboard shortcuts", () => new KeyboardShortcutsDialog(new KeyboardShortcutsViewModel(registry))),
            ("Trust", () => new TrustDialog("/p/P.csproj", ["/p/ext"])),
            ("Issues", () => new IssuesDialog("Extension issues", Issues())),
            ("Recover", () => new RecoverDialog([new RecoveryFile("/p/Program.netpc.json", new DateTime(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc), true)])),
            ("About", () => new AboutDialog(new AboutViewModel("1.2.3"))),
            ("Error", () => new ErrorDialog("Failed to run", "The program stopped.")),
            ("Override method", () => new SelectMethodDialog(OverrideCandidates(session.App.Composition.Context.Reflection.Provider), new HashSet<string>(["ToString"], StringComparer.Ordinal))),
        ];
        Window[] dialogWindows = [.. dialogs.Select(dialog => dialogUi.Show(dialog.Create()))];
        Window mark = markUi.Show(MarkWindow());
        Window[] windows = [session.Window, start.Window, mark, .. dialogWindows];

        (string Label, UiImage?[] Cells)[] rows =
        [
            .. new[] { "Project tree", "Inspector", "Variables", "Errors", "Output", "C#", "Start page", "Graph document" }
                .Concat(dialogs.Select(dialog => dialog.Name)).Append("Overloads flyout")
                .Select(label => (label, new UiImage?[Variants.Length])),
        ];
        Assert.Equal(Rows, rows.Length);

        try
        {
            for (int v = 0; v < Variants.Length; v++)
            {
                Apply(Variants[v].Variant, Variants[v].Scale, windows);
                double scale = Variants[v].Scale;
                var bottom = session.Page.Bottom;
                int row = 0;
                rows[row++].Cells[v] = Cell(await CropAsync(session.Driver, session.Page.Tree, scale, topLeftOnly: true), scale);
                rows[row++].Cells[v] = Cell(await CropAsync(session.Driver, session.Page.Inspector, scale, topLeftOnly: true), scale);
                rows[row++].Cells[v] = Cell(await CropAsync(session.Driver, session.Page.Variables, scale, topLeftOnly: true), scale);
                foreach (string panel in new[] { PanelContributions.ErrorsId, PanelContributions.OutputId, PanelContributions.CSharpId })
                {
                    await bottom.ShowAsync(panel, Token);
                    rows[row++].Cells[v] = Cell(await CropAsync(session.Driver, bottom.Panel(panel), scale, topLeftOnly: true), scale);
                }

                rows[row++].Cells[v] = Cell(Frame(start.Window), scale);
                rows[row++].Cells[v] = Cell(await CropAsync(session.Driver, session.Graph, scale), scale);
                foreach (Window dialog in dialogWindows)
                {
                    rows[row++].Cells[v] = Cell(Frame(dialog), scale);
                }

                await session.Graph.Node("CallMethodNode").Overloads.ClickAsync(Token);
                rows[row++].Cells[v] = Cell(await FlyoutAsync(session, scale), scale);
                await session.Driver.PressAsync("Esc", Token);
            }

            Apply(ThemeVariant.Dark, 1.0, windows);
            var view = session.Window.GetVisualDescendants().OfType<GraphEditorView>().First();
            CurrentApplication.TryGetResource("Pin.TypeShape.Triangle", CurrentApplication.ActualThemeVariant, out object? triangle);
            Assert.NotNull(triangle);
            void Triangle(ResourceDictionary resources) => resources["Pin.TypeShape"] = triangle;
            void Canvas(ResourceDictionary resources) => resources["Canvas.Background"] = Color.Parse("#FF000000");
            void Card(ResourceDictionary resources) => resources["Node.CardBackground"] = new SolidColorBrush(Color.Parse("#AA202020"));
            (string Label, Action<ResourceDictionary> Overrides)[] calls =
            [
                ("New defaults (Dark): diamond pins, canvas #262626, card #D9141414", _ => { }),
                ("Classic Dark, pins only: triangle", Triangle),
                ("Classic Dark, canvas only: #000", Canvas),
                ("Classic Dark, card only: #AA202020", Card),
                ("Classic Dark, all three: triangle, #000, #AA202020", resources => { Triangle(resources); Canvas(resources); Card(resources); }),
            ];
            List<ContactTile> strip = [];
            foreach ((string label, Action<ResourceDictionary> overrides) in calls)
            {
                strip.Add(new ContactTile(label, await GraphWithAsync(session, view, overrides)));
            }

            for (int v = 0; v < MarkTiles; v++)
            {
                Apply(Variants[v].Variant, 1.0, windows);
                strip.Add(new ContactTile($"Product mark, {Variants[v].Name}", Cell(Frame(mark))));
            }

            var sections = Sections(rows, strip);
            int tiles = sections.Sum(section => section.Tiles.Count);
            Assert.Equal(Rows * 2 * 2 + OwnerCalls + MarkTiles, tiles);
            Assert.All(sections.SelectMany(section => section.Tiles), tile => Assert.False(IsFlat(tile.Image), $"{tile.Label} is one flat colour"));

            ContactSheetComposer.Compose(sections).Save(Path.Combine(UiArtifacts.Directory, "contact-sheet.png"));
        }
        finally
        {
            Apply(ThemeVariant.Dark, 1.0, windows);
        }
    }
}
