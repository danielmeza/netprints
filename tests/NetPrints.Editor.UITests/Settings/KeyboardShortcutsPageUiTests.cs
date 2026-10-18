using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Settings;
using NetPrints.Editor.Shell;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Shell;
using NetPrints.Extensibility.Settings;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Settings;

/// <summary>US6: the Keyboard shortcuts page records a key chord from the keyboard and shows the gesture at once.</summary>
public class KeyboardShortcutsPageUiTests
{
    private const string Chord = "Ctrl+Alt+Shift+Q";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static IReadOnlyList<Control> Find(ShellApp app, string automationId, string? name = null) =>
        [.. app.Ui.Tree.FindControls(new AutomationQuery(automationId) { Name = name }).Select(pair => pair.Control)];

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AddGestureRecordsTheNextChordAndTheRowShowsItAtOnce()
    {
        await using ShellApp app = ShellApp.Start();
        Assert.True(app.Commands.TryRun("openSettings"));
        await UiWait.UntilAsync(app.Driver, () => Task.FromResult(app.Api.OpenDocuments.Contains(DocumentId.Settings)), "settings open", Token);
        var document = Assert.IsType<SettingsDocumentViewModel>(app.Shell.FindDocument(DocumentId.Settings));
        Assert.True(document.SelectPage(SettingsDocumentViewModel.KeyboardPageId));
        HeadlessDriver.Pump();
        Control row = Assert.Single(Find(app, AutomationIds.ShortcutsEditRow, "Settings"));
        var add = Assert.IsType<Button>(Assert.Single(row.GetVisualDescendants().OfType<Button>(), button => Avalonia.Automation.AutomationProperties.GetAutomationId(button) == AutomationIds.ShortcutsAdd));

        add.Focus();
        add.Command?.Execute(null);
        HeadlessDriver.Pump();
        await app.Driver.PressAsync(Chord, Token);
        await UiWait.UntilAsync(app.Driver, () => Task.FromResult(app.Composition.Context.Settings.Get(NetPrints.Editor.State.KeyboardSettings.Descriptor).Bindings.Count == 1), "stored", Token);
        HeadlessDriver.Pump();

        var item = Assert.IsType<KeyboardShortcutsItemViewModel>(document.SelectedPage?.Items.Single());
        Assert.Contains("Q", item.Rows.Single(candidate => candidate.Label == "Settings").Current, StringComparison.Ordinal);
    }
}
