using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Styling;
using Dock.Model;
using Dock.Model.Controls;
using Dock.Model.Core;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Hosting.Avalonia;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The docking adapter through <see cref="IShell"/>: documents, panels, floating and the default layout (contracts/shell.md section 1).</summary>
public class ShellAdapterTests
{
    private static readonly DocumentId A = DocumentId.Graph("A.cs", "method:1");
    private static readonly DocumentId B = DocumentId.Graph("A.cs", "method:2");
    private static readonly DocumentId C = DocumentId.Graph("A.cs", "method:3");

    private static readonly string[] PanelIds =
    [
        PanelContributions.ProjectTreeId, PanelContributions.InspectorId, PanelContributions.ErrorsId, PanelContributions.OutputId, PanelContributions.CSharpId,
    ];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void OpeningADocumentTwiceKeepsItsOneTab()
    {
        using var rig = ShellRig.Create();

        rig.Api.OpenDocument(A);
        rig.Api.OpenDocument(A);
        rig.Settle();

        Assert.Equal([A], rig.Api.OpenDocuments);
        Assert.Equal(A, rig.Api.ActiveDocument);
        Assert.Same(rig.Shell.ActiveDocument, Assert.Single(rig.Shell.Documents));
        Assert.Equal(A, rig.Shell.ActiveDocument?.Id);
        Assert.NotNull(rig.FindOne(ShellRig.Content(A)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ADocumentTheAppDoesNotKnowIsNotOpened()
    {
        using var rig = ShellRig.Create(_ => null);

        rig.Api.OpenDocument(A);
        rig.Settle();

        Assert.Empty(rig.Api.OpenDocuments);
        Assert.Null(rig.Api.ActiveDocument);
        Assert.Empty(rig.Shell.Documents);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ActivatingATabShowsItsContentAndKeepsTheShellInStep()
    {
        using var rig = ShellRig.Create();
        rig.Api.OpenDocument(A);
        rig.Api.OpenDocument(B);
        rig.Settle();
        Assert.Equal(B, rig.Api.ActiveDocument);
        Assert.NotNull(rig.FindOne(ShellRig.Content(B)));
        Assert.Null(rig.FindOne(ShellRig.Content(A)));

        rig.Api.ActivateDocument(A);
        rig.Settle();

        Assert.Equal(A, rig.Api.ActiveDocument);
        Assert.Equal(A, rig.Shell.ActiveDocument?.Id);
        Assert.NotNull(rig.FindOne(ShellRig.Content(A)));
        Assert.Null(rig.FindOne(ShellRig.Content(B)));
        Assert.Equal([A, B], rig.Api.OpenDocuments);

        rig.Api.ActivateDocument(C);
        Assert.Equal(A, rig.Api.ActiveDocument);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ClosingADocumentRemovesItsTabAndActivatesANeighbour()
    {
        using var rig = ShellRig.Create();
        rig.Api.OpenDocument(A);
        rig.Api.OpenDocument(B);
        rig.Settle();

        rig.Api.CloseDocument(B);
        rig.Settle();

        Assert.Equal([A], rig.Api.OpenDocuments);
        Assert.Equal(A, rig.Api.ActiveDocument);
        Assert.Equal(A, rig.Shell.ActiveDocument?.Id);
        Assert.Same(rig.Shell.FindDocument(A), Assert.Single(rig.Shell.Documents));
        Assert.NotNull(rig.FindOne(ShellRig.Content(A)));

        rig.Api.CloseDocument(A);
        rig.Settle();
        Assert.Empty(rig.Api.OpenDocuments);
        Assert.Null(rig.Api.ActiveDocument);
        Assert.Null(rig.Shell.ActiveDocument);

        rig.Api.OpenDocument(B);
        rig.Settle();
        Assert.Equal([B], rig.Api.OpenDocuments);
        Assert.NotNull(rig.FindOne(ShellRig.Content(B)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void EveryPanelIsVisibleAtFirstAndHidingOneLeavesTheOthers()
    {
        using var rig = ShellRig.Create();
        rig.Settle();
        Assert.All(PanelIds, id => Assert.True(rig.Api.IsPanelVisible(id), id));

        rig.Api.HidePanel(PanelContributions.InspectorId);
        rig.Settle();

        Assert.False(rig.Api.IsPanelVisible(PanelContributions.InspectorId));
        Assert.False(rig.Shell.Panels.Single(p => p.Id == PanelContributions.InspectorId).IsVisible);
        Assert.Null(rig.FindOne(ShellRig.Panel(PanelContributions.InspectorId)));
        Assert.All(PanelIds.Where(id => id != PanelContributions.InspectorId), id => Assert.True(rig.Api.IsPanelVisible(id), id));
        Assert.NotNull(rig.FindOne(ShellRig.Panel(PanelContributions.ProjectTreeId)));

        rig.Api.ShowPanel(PanelContributions.InspectorId);
        rig.Settle();

        Assert.True(rig.Api.IsPanelVisible(PanelContributions.InspectorId));
        Assert.True(rig.Shell.Panels.Single(p => p.Id == PanelContributions.InspectorId).IsVisible);
        Assert.NotNull(rig.FindOne(ShellRig.Panel(PanelContributions.InspectorId)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void OpeningADocumentWithoutADocumentDockKeepsTheLayoutAndActivatesTheDocument()
    {
        using var rig = ShellRig.Create();
        rig.Api.HidePanel(PanelContributions.ErrorsId);
        IDocumentDock dock = Assert.Single(ShellDockFactory.Walk(rig.Adapter.Layout).OfType<IDocumentDock>());
        RemoveFromLayout(rig, dock);
        rig.Settle();

        rig.Api.OpenDocument(A);
        rig.Settle();

        Assert.False(rig.Api.IsPanelVisible(PanelContributions.ErrorsId));
        Assert.Equal([A], rig.Api.OpenDocuments);
        Assert.Equal(A, rig.Api.ActiveDocument);
    }

    private static void RemoveFromLayout(ShellRig rig, IDockable dock)
    {
        IDock owner = Assert.IsAssignableFrom<IDock>(dock.Owner);
        IList<IDockable>? siblings = owner.VisibleDockables;
        Assert.NotNull(siblings);
        Assert.True(siblings.Remove(dock));
        rig.Settle();
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AnUnknownPanelIsNeitherShownNorHidden()
    {
        using var rig = ShellRig.Create();

        rig.Api.HidePanel("acme.panel.missing");
        rig.Api.ShowPanel("acme.panel.missing");

        Assert.False(rig.Api.IsPanelVisible("acme.panel.missing"));
        Assert.All(PanelIds, id => Assert.True(rig.Api.IsPanelVisible(id), id));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ADocumentFloatsIntoItsOwnWindowAndDocksBack()
    {
        using var rig = ShellRig.Create();
        rig.Api.OpenDocument(A);
        rig.Api.OpenDocument(B);
        rig.Settle();
        Assert.False(rig.Api.IsFloating(B));

        rig.Api.FloatDocument(B);
        rig.Settle();

        Assert.True(rig.Api.IsFloating(B));
        Assert.False(rig.Api.IsFloating(A));
        Assert.Equal(2, rig.Ui.Tree.Windows.Count);
        Assert.NotEqual(rig.MainKey, Assert.Single(rig.Ui.Tree.Find(ShellRig.Content(B))).Window);
        Assert.Equal([A, B], rig.Api.OpenDocuments.Order(Comparer<DocumentId>.Create((x, y) => string.CompareOrdinal(x.ToString(), y.ToString()))));

        rig.Api.DockDocument(B);
        rig.Settle();

        Assert.False(rig.Api.IsFloating(B));
        Assert.Single(rig.Ui.Tree.Windows);
        Assert.Equal(rig.MainKey, Assert.Single(rig.Ui.Tree.Find(ShellRig.Content(B))).Window);
        Assert.Equal(2, rig.Api.OpenDocuments.Count);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ResetLayoutRestoresTheDefaultLayoutAndKeepsTheOpenDocuments()
    {
        using var rig = ShellRig.Create();
        rig.Api.OpenDocument(A);
        rig.Api.OpenDocument(B);
        rig.Api.HidePanel(PanelContributions.InspectorId);
        rig.Api.HidePanel(PanelContributions.ErrorsId);
        rig.Api.FloatDocument(B);
        rig.Adapter.FloatPanel(PanelContributions.ProjectTreeId);
        rig.Settle();

        rig.Api.ResetLayout();
        rig.Settle();

        Assert.All(PanelIds, id => Assert.True(rig.Api.IsPanelVisible(id), id));
        Assert.All(rig.Shell.Panels, panel => Assert.True(panel.IsVisible, panel.Id));
        Assert.Single(rig.Ui.Tree.Windows);
        Assert.False(rig.Api.IsFloating(B));
        Assert.Equal(2, rig.Api.OpenDocuments.Count);
        Assert.Equal(B, rig.Api.ActiveDocument);
        AssertDefaultGeometry(rig);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheDefaultLayoutHasTheTreeOnTheLeftTheInspectorOnTheRightAndErrorsActiveAtTheBottom()
    {
        using var rig = ShellRig.Create();
        rig.Settle();

        AssertDefaultGeometry(rig);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ActivatingAFloatedDocumentBringsItsWindowForward()
    {
        using var rig = ShellRig.Create();
        rig.Api.OpenDocument(A);
        rig.Api.OpenDocument(B);
        rig.Api.FloatDocument(B);
        rig.Settle();
        IDockWindow floating = Assert.Single(rig.Adapter.Layout.Windows ?? []);
        var spy = new ActivationSpy(floating.Host ?? throw new InvalidOperationException("The floating window has no host."));
        floating.Host = spy;

        rig.Api.ActivateDocument(A);
        Assert.Equal(0, spy.Activations);

        rig.Api.ActivateDocument(B);

        Assert.Equal(1, spy.Activations);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void DialogsAreOwnedByTheActiveDockHostWindow()
    {
        using var rig = ShellRig.Create();
        rig.Api.OpenDocument(A);
        rig.Api.OpenDocument(B);
        rig.Api.FloatDocument(B);
        rig.Settle();
        Window host = Assert.Single(rig.Ui.Tree.Windows, window => !ReferenceEquals(window, rig.Main));
        var windows = new WindowService { MainWindow = rig.Main, OpenWindows = () => rig.Ui.Tree.Windows };

        rig.Main.Hide();
        rig.Settle();
        Assert.Same(host, windows.ActiveWindow);

        host.Hide();
        rig.Main.Show();
        rig.Settle();
        Assert.Same(rig.Main, windows.ActiveWindow);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AFloatedPaneClosedWithTheOsCloseButtonDocksBackToItsDefaultPlace()
    {
        using var rig = ShellRig.Create();
        rig.Settle();

        rig.Adapter.FloatPanel(PanelContributions.InspectorId);
        rig.Settle();
        var floating = Assert.Single(rig.Ui.Tree.Find(ShellRig.Panel(PanelContributions.InspectorId)));
        Assert.NotEqual(rig.MainKey, floating.Window);
        Window host = Assert.Single(rig.Ui.Tree.Windows, window => !ReferenceEquals(window, rig.Main));
        host.Close();
        rig.Settle();

        Assert.Single(rig.Ui.Tree.Windows);
        Assert.True(rig.Api.IsPanelVisible(PanelContributions.InspectorId));
        AssertDefaultGeometry(rig);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AFloatedGraphTabClosesLikeATabDoes()
    {
        using var rig = ShellRig.Create();
        rig.Api.OpenDocument(A);
        rig.Api.OpenDocument(B);
        rig.Api.FloatDocument(B);
        rig.Settle();
        Window host = Assert.Single(rig.Ui.Tree.Windows, window => !ReferenceEquals(window, rig.Main));

        host.Close();
        rig.Settle();

        Assert.Single(rig.Ui.Tree.Windows);
        Assert.Equal([A], rig.Api.OpenDocuments);
        Assert.Same(rig.Shell.FindDocument(A), Assert.Single(rig.Shell.Documents));
        Assert.Equal(A, rig.Api.ActiveDocument);
        Assert.All(PanelIds, id => Assert.True(rig.Api.IsPanelVisible(id), id));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AutomationIdsAreFoundInDockedTabbedAndFloatingPanesByTheAutomationPipe()
    {
        using var rig = ShellRig.Create();
        rig.Api.OpenDocument(A);
        rig.Api.OpenDocument(B);
        rig.Api.ActivateDocument(A);
        rig.Settle();
        string pipe = Path.Combine(Path.GetTempPath(), "netprints-shell-" + Guid.NewGuid().ToString("N"));
        using var agent = new AutomationAgent(pipe, rig.Ui.Tree, () => new AutomationStatus(true, false, false, null, Environment.ProcessId), NullLogger<AutomationAgent>.Instance);
        await using var client = await AutomationClient.ConnectAsync(pipe, TimeSpan.FromSeconds(10), Token);

        Assert.Equal(rig.MainKey, Assert.Single(await client.FindAsync(ShellRig.Panel(PanelContributions.ErrorsId), Token)).Window);
        Assert.Equal(rig.MainKey, Assert.Single(await client.FindAsync(ShellRig.Content(A), Token)).Window);
        Assert.Empty(await client.FindAsync(ShellRig.Content(B), Token));

        rig.Api.FloatDocument(A);
        rig.Settle();
        var floating = Assert.Single(await client.FindAsync(ShellRig.Content(A), Token));
        Assert.NotEqual(rig.MainKey, floating.Window);
        Assert.Contains(await client.TreeAsync(Token), element => element.AutomationId == AutomationIds.ShellDocumentPrefix + A && element.Window == floating.Window);

        rig.Api.DockDocument(A);
        rig.Settle();
        Assert.Equal(rig.MainKey, Assert.Single(await client.FindAsync(ShellRig.Content(A), Token)).Window);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheTemplatesRenderNetPrintsViewModelsWithoutBindingWarningsOfTheirOwn()
    {
        BindingWarningLogSink warnings = RenderLayoutWarnings(rig =>
        {
            rig.Api.OpenDocument(A);
            rig.Settle();
            Assert.NotNull(rig.FindOne(ShellRig.Content(A)));
            Assert.NotNull(rig.FindOne(ShellRig.Panel(PanelContributions.ErrorsId)));
        });

        Assert.Empty(warnings.Warnings);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void FloatedTabsAndPanesRenderWithoutBindingWarningsOfTheirOwn()
    {
        BindingWarningLogSink warnings = RenderLayoutWarnings(rig =>
        {
            rig.Api.OpenDocument(A);
            rig.Api.OpenDocument(B);
            rig.Api.FloatDocument(B);
            rig.Adapter.FloatPanel(PanelContributions.ProjectTreeId);
            rig.Settle();
            Assert.Equal(2, rig.Ui.Tree.Windows.Count(window => !ReferenceEquals(window, rig.Main)));
            Assert.NotNull(rig.FindOne(ShellRig.Content(B)));
            Assert.NotNull(rig.FindOne(ShellRig.Panel(PanelContributions.ProjectTreeId)));
        });

        Assert.Empty(warnings.Unexplained);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AnEmptyCapabilityObjectStillInheritsSoDragDropAndCloseStayAllowed()
    {
        using var rig = ShellRig.Create();
        rig.Api.OpenDocument(A);
        rig.Api.OpenDocument(B);
        rig.Api.FloatDocument(B);
        rig.Adapter.FloatPanel(PanelContributions.ProjectTreeId);
        rig.Settle();

        IDockable[] all = [.. ShellDockFactory.Walk(rig.Adapter.Layout)];
        Assert.NotEmpty(all.OfType<ShellDocument>());
        Assert.NotEmpty(all.OfType<ShellTool>());
        Assert.All(all.OfType<ShellDocument>().Cast<IDockable>().Concat(all.OfType<ShellTool>()), dockable =>
        {
            Assert.NotNull(dockable.DockCapabilityOverrides);
            Assert.False(dockable.DockCapabilityOverrides.HasAnyOverride);
        });
        Assert.All(all.OfType<IDock>().Where(dock => dock is IDocumentDock or IToolDock), dock => Assert.NotNull(dock.DockCapabilityPolicy));
        Assert.All(all.OfType<ShellDocument>().Cast<IDockable>().Concat(all.OfType<ShellTool>()), dockable =>
        {
            Assert.True(DockCapabilityResolver.IsEnabled(dockable, DockCapability.Drag, null), $"{dockable.Id} drag");
            Assert.True(DockCapabilityResolver.IsEnabled(dockable, DockCapability.Drop, null), $"{dockable.Id} drop");
            Assert.True(DockCapabilityResolver.IsEnabled(dockable, DockCapability.Close, null), $"{dockable.Id} close");
        });

        rig.Api.CloseDocument(B);
        rig.Settle();
        Assert.Equal([A], rig.Api.OpenDocuments);
    }

    private static BindingWarningLogSink RenderLayoutWarnings(Action<ShellRig> scenario)
    {
        var sink = new BindingWarningLogSink();
        ILogSink? previousSink = Logger.Sink;
        Logger.Sink = sink;
        using var rig = ShellRig.Create();
        try
        {
            scenario(rig);
        }
        finally
        {
            Logger.Sink = previousSink;
        }

        return sink;
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("Dark", 0xFF333333u)]
    [InlineData("Light", 0xFFE6E6E6u)]
    public void TheThemeVariantSwitchesTheDockSurfaceTokens(string variantName, uint expected)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var application = Application.Current ?? throw new InvalidOperationException("No application.");
        ThemeVariant previous = application.RequestedThemeVariant ?? ThemeVariant.Dark;
        application.RequestedThemeVariant = variant;
        try
        {
            using var rig = ShellRig.Create();
            rig.Api.OpenDocument(A);
            rig.Api.FloatDocument(A);
            rig.Settle();

            Assert.True(rig.Main.TryFindResource("SystemChromeMediumColor", variant, out object? chrome));
            Assert.Equal(Color.FromUInt32(expected), Assert.IsType<Color>(chrome));
            Assert.True(rig.Main.TryFindResource("DockSurfaceHeaderBrush", variant, out object? header));
            Assert.Equal((Color)chrome, Assert.IsAssignableFrom<ISolidColorBrush>(header).Color);
            Window host = Assert.Single(rig.Ui.Tree.Windows, window => !ReferenceEquals(window, rig.Main));
            Assert.Equal((Color)chrome, Assert.IsAssignableFrom<ISolidColorBrush>(host.Background).Color);
        }
        finally
        {
            application.RequestedThemeVariant = previous;
        }
    }

    private static void AssertDefaultGeometry(ShellRig rig)
    {
        var tree = Assert.Single(rig.Ui.Tree.Find(ShellRig.Panel(PanelContributions.ProjectTreeId)));
        var inspector = Assert.Single(rig.Ui.Tree.Find(ShellRig.Panel(PanelContributions.InspectorId)));
        var errors = Assert.Single(rig.Ui.Tree.Find(ShellRig.Panel(PanelContributions.ErrorsId)));
        Assert.Equal(rig.MainKey, tree.Window);
        Assert.Equal(rig.MainKey, inspector.Window);
        Assert.Equal(rig.MainKey, errors.Window);

        // The bottom tabs share one pane and Errors is the active one: the others are not realised.
        Assert.Null(rig.FindOne(ShellRig.Panel(PanelContributions.OutputId)));
        Assert.Null(rig.FindOne(ShellRig.Panel(PanelContributions.CSharpId)));

        Assert.InRange(tree.Bounds.Width / ShellRig.Width, 0.15, 0.20);
        Assert.InRange(inspector.Bounds.Width / ShellRig.Width, 0.17, 0.22);
        Assert.InRange(errors.Bounds.Height / ShellRig.Height, 0.18, 0.25);
        Assert.True(tree.Bounds.X < inspector.Bounds.X, "the tree is on the left of the inspector");
        Assert.True(inspector.Bounds.X > ShellRig.Width * 0.7, "the inspector is on the right");
        Assert.True(errors.Bounds.Y > tree.Bounds.Y + tree.Bounds.Height - 1, "the bottom pane is below the tree");
    }

    private sealed class ActivationSpy(IHostWindow inner) : IHostWindow
    {
        public int Activations { get; private set; }

        public IHostWindowState? HostWindowState => inner.HostWindowState;

        public bool IsTracked { get => inner.IsTracked; set => inner.IsTracked = value; }

        public IDockWindow? Window { get => inner.Window; set => inner.Window = value; }

        public void Present(bool isDialog) => inner.Present(isDialog);

        public void Exit() => inner.Exit();

        public void SetPosition(double x, double y) => inner.SetPosition(x, y);

        public void GetPosition(out double x, out double y) => inner.GetPosition(out x, out y);

        public void SetSize(double width, double height) => inner.SetSize(width, height);

        public void GetSize(out double width, out double height) => inner.GetSize(out width, out height);

        public void SetWindowState(DockWindowState windowState) => inner.SetWindowState(windowState);

        public DockWindowState GetWindowState() => inner.GetWindowState();

        public void SetTitle(string? title) => inner.SetTitle(title);

        public void SetLayout(IDock layout) => inner.SetLayout(layout);

        public void SetActive()
        {
            Activations++;
            inner.SetActive();
        }
    }
}
