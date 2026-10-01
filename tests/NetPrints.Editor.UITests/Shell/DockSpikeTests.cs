using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Styling;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking.Spike;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>ADR-0018 spike, checks 1, 2, 3 and 5 on the headless platform (check 4, floating under openbox, is the E2E spike).</summary>
public class DockSpikeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static AutomationQuery ContentId(string text) => new(AutomationIds.DockSpikeContentId) { Text = text };

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void Check1_DocumentTemplateRendersANetPrintsViewModel()
    {
        var sink = new BindingWarningLogSink();
        ILogSink? previousSink = Logger.Sink;
        Logger.Sink = sink;
        try
        {
            using var ui = HeadlessUi.Create();
            ui.Show(new DockSpikeWindow());

            var document = Assert.Single(ui.Tree.Find(ContentId("NPT-A")));
            Assert.Equal("First document", Assert.Single(ui.Tree.Find(new AutomationQuery(AutomationIds.DockSpikeContentMessage) { Text = "First document" })).Text);
            Assert.Equal("TextBlock", document["Type"]);
            Assert.Single(ui.Tree.Find(ContentId("NPT-TOOL")));
        }
        finally
        {
            Logger.Sink = previousSink;
        }

        // Dock's own theme templates log "Value is null" for their Layout and capability bindings; none comes from the NetPrints templates.
        Assert.All(sink.Messages, message => Assert.Matches("Layout\\.|DockCapability", message));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void Check2_ALayoutRoundTripRecreatesItsDocumentsByStableId()
    {
        var factory = new SpikeDockFactory();
        var store = new SpikeLayoutStore();
        var layout = factory.CreateLayout();
        factory.InitLayout(layout);
        factory.FloatDockable(SpikeDockFactory.Walk(layout).First(d => d.Id == SpikeDockFactory.SecondDocumentId));

        using var saving = new MemoryStream();
        store.Save(saving, layout);
        byte[] saved = saving.ToArray();
        string json = Encoding.UTF8.GetString(saved).TrimStart('\uFEFF');
        Assert.Contains(SpikeDockFactory.FirstDocumentId, json, StringComparison.Ordinal);
        Assert.Contains(SpikeDockFactory.SecondDocumentId, json, StringComparison.Ordinal);
        Assert.DoesNotContain("NPT-A", json, StringComparison.Ordinal); // the view models are not persisted, only the ids
        using (JsonDocument.Parse(json))
        {
        }

        // Every document still exists: re-attached through the app, floating one included.
        var loaded = store.Load(new MemoryStream(saved), new SpikeDockFactory(), SpikeDockFactory.Resolve);
        Assert.NotNull(loaded);
        var documents = SpikeDockFactory.Walk(loaded).OfType<SpikeDocument>().ToList();
        Assert.Equal([SpikeDockFactory.FirstDocumentId, SpikeDockFactory.SecondDocumentId], documents.Select(d => d.Id).Order(StringComparer.Ordinal));
        Assert.All(documents, d => Assert.NotNull(d.Context));
        Assert.NotNull(Assert.Single(loaded.Windows ?? []).Factory);

        // The graph behind the second document is gone: it is dropped, the rest survives.
        var pruned = store.Load(new MemoryStream(saved), new SpikeDockFactory(), id => id == SpikeDockFactory.SecondDocumentId ? null : SpikeDockFactory.Resolve(id));
        Assert.NotNull(pruned);
        Assert.Equal([SpikeDockFactory.FirstDocumentId], SpikeDockFactory.Walk(pruned).OfType<SpikeDocument>().Select(d => d.Id));

        // And the loaded layout renders its re-attached view models; the floating window opens once the dock control is attached.
        var rendered = new SpikeDockFactory();
        var layoutToShow = store.Load(new MemoryStream(saved), rendered, SpikeDockFactory.Resolve);
        Assert.NotNull(layoutToShow);
        using var ui = HeadlessUi.Create();
        var main = ui.Show(new DockSpikeWindow(new DockSpikeViewModel(rendered, layoutToShow)));
        rendered.InitLayout(layoutToShow);
        HeadlessDriver.Pump();
        string mainKey = ui.Tree.KeyOf(main);
        Assert.Equal(mainKey, Assert.Single(ui.Tree.Find(ContentId("NPT-A"))).Window);
        Assert.NotEqual(mainKey, Assert.Single(ui.Tree.Find(ContentId("NPT-B"))).Window);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task Check3_AutomationIdsAreFoundInDockedTabbedAndFloatingPanes()
    {
        var viewModel = new DockSpikeViewModel(new SpikeDockFactory());
        using var ui = HeadlessUi.Create();
        var main = ui.Show(new DockSpikeWindow(viewModel));
        string mainKey = ui.Tree.KeyOf(main);
        string pipe = Path.Combine(Path.GetTempPath(), "netprints-spike-" + Guid.NewGuid().ToString("N"));
        using var agent = new AutomationAgent(pipe, ui.Tree, () => new AutomationStatus(true, false, false, null, Environment.ProcessId), NullLogger<AutomationAgent>.Instance);
        await using var client = await AutomationClient.ConnectAsync(pipe, TimeSpan.FromSeconds(10), Token);

        // Docked: the tool pane. Tabbed: only the active tab's content is realized.
        Assert.Equal(mainKey, Assert.Single(await client.FindAsync(ContentId("NPT-TOOL"), Token)).Window);
        Assert.Equal(mainKey, Assert.Single(await client.FindAsync(ContentId("NPT-A"), Token)).Window);
        Assert.Empty(await client.FindAsync(ContentId("NPT-B"), Token));

        var second = SpikeDockFactory.Walk(viewModel.Layout).First(d => d.Id == SpikeDockFactory.SecondDocumentId);
        viewModel.Factory.SetActiveDockable(second);
        HeadlessDriver.Pump();
        Assert.Equal(mainKey, Assert.Single(await client.FindAsync(ContentId("NPT-B"), Token)).Window);
        Assert.Empty(await client.FindAsync(ContentId("NPT-A"), Token));

        // Floating: the content moves to a window of its own, found by the same tree.
        viewModel.Factory.FloatDockable(second);
        HeadlessDriver.Pump();
        Assert.Equal(2, ui.Tree.Windows.Count);
        var floating = Assert.Single(await client.FindAsync(ContentId("NPT-B"), Token));
        Assert.NotEqual(mainKey, floating.Window);
        Assert.Contains(await client.TreeAsync(Token), e => e.AutomationId == AutomationIds.DockSpikeContentId && e.Window == floating.Window);
        Assert.Single(await client.FindAsync(ContentId("NPT-A"), Token));
        Assert.Single(await client.FindAsync(ContentId("NPT-TOOL"), Token));

        // Re-docked: back in the main window.
        viewModel.DockDocumentCommand.Execute(null);
        HeadlessDriver.Pump();
        Assert.Equal(mainKey, Assert.Single(await client.FindAsync(ContentId("NPT-B"), Token)).Window);
        Assert.Single(ui.Tree.Windows);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [InlineData("Dark", 0xFF333333u)]
    [InlineData("Light", 0xFFE6E6E6u)]
    public async Task Check5_TheThemeVariantSwitchesTheDockTokens(string variantName, uint expected)
    {
        var variant = variantName == "Dark" ? ThemeVariant.Dark : ThemeVariant.Light;
        var application = Application.Current ?? throw new InvalidOperationException("No application.");
        ThemeVariant previous = application.RequestedThemeVariant ?? ThemeVariant.Dark;
        application.RequestedThemeVariant = variant;
        try
        {
            using var ui = HeadlessUi.Create();
            var window = ui.Show(new DockSpikeWindow());
            var swatch = Assert.Single(ui.Tree.FindControls(new AutomationQuery(AutomationIds.DockSpikeSwatch))).Control;
            HeadlessDriver.Pump();

            var brush = Assert.IsAssignableFrom<ISolidColorBrush>(swatch is Border border ? border.Background : null);
            Assert.Equal(Color.FromUInt32(expected), brush.Color);
            Assert.True(window.TryFindResource("DockSurfaceHeaderBrush", variant, out object? header));
            Assert.Equal(Color.FromUInt32(expected), Assert.IsAssignableFrom<ISolidColorBrush>(header).Color);

            var element = Assert.Single(ui.Tree.Find(new AutomationQuery(AutomationIds.DockSpikeSwatch)));
            var image = await ui.Driver.ScreenshotAsync(element.Window, Token);
            uint pixel = image.Pixel((int)(element.Bounds.X + element.Bounds.Width / 2), (int)(element.Bounds.Y + element.Bounds.Height / 2));
            Assert.Equal(expected, pixel);

            // Dock's own chrome follows the same token: a floating host window paints DockSurfaceEditorBrush.
            var viewModel = Assert.IsType<DockSpikeViewModel>(window.DataContext);
            viewModel.FloatDocumentCommand.Execute(null);
            HeadlessDriver.Pump();
            var host = Assert.Single(ui.Tree.Windows, w => !ReferenceEquals(w, window));
            Assert.Equal(Color.FromUInt32(expected), Assert.IsAssignableFrom<ISolidColorBrush>(host.Background).Color);
        }
        finally
        {
            application.RequestedThemeVariant = previous;
        }
    }
}
