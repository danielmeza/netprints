using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Compilation;
using NetPrints.Editor.Commands.KeyboardShortcuts;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Controls;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Icons;
using NetPrints.Editor.Lifecycle;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Dialogs;

/// <summary>The eight P3a dialogs are hosted in the dialog shell: title, icon, platform button order, Enter and Esc, width (FR-088).</summary>
public class DialogShellTests
{
    private sealed record Row(string Name, Func<Window> Create, string IconId, string[] CancelFirst, string DefaultId, string CancelId);

    private static CancellationToken Cancel => TestContext.Current.CancellationToken;

    private static readonly Dictionary<string, Row> Rows = new Row[]
    {
        new("Unsaved", () => new UnsavedChangesDialog([new UnsavedFile("/p/A.cs", UnsavedFileKind.Class, "A")]), IconIds.DialogWarning,
            [AutomationIds.UnsavedCancelButton, AutomationIds.UnsavedDontSaveButton, AutomationIds.UnsavedSaveAllButton],
            AutomationIds.UnsavedSaveAllButton, AutomationIds.UnsavedCancelButton),
        new("Confirm", () => new ConfirmDialog("Delete it", "This cannot be undone.", "Delete"), IconIds.DialogConfirm,
            [AutomationIds.ConfirmCancelButton, AutomationIds.ConfirmButton], AutomationIds.ConfirmButton, AutomationIds.ConfirmCancelButton),
        new("Shortcuts", () => new KeyboardShortcutsDialog(new KeyboardShortcutsViewModel(Registry())), IconIds.DialogInfo,
            [AutomationIds.ShortcutsCloseButton], AutomationIds.ShortcutsCloseButton, AutomationIds.ShortcutsCloseButton),
        new("Trust", () => new TrustDialog("/p/P.csproj", ["/p/ext"]), IconIds.DialogTrust,
            [AutomationIds.TrustDontLoadButton, AutomationIds.TrustButton], AutomationIds.TrustDontLoadButton, AutomationIds.TrustDontLoadButton),
        new("Issues", () => new IssuesDialog("Extension issues", []), IconIds.DialogWarning,
            [AutomationIds.IssuesOkButton], AutomationIds.IssuesOkButton, AutomationIds.IssuesOkButton),
        new("Recover", () => new RecoverDialog([]), IconIds.DialogRecover,
            [AutomationIds.RecoverDiscardButton, AutomationIds.RecoverRestoreButton], AutomationIds.RecoverRestoreButton, AutomationIds.RecoverDiscardButton),
        new("About", () => new AboutDialog(new AboutViewModel("1.2.3")), IconIds.DialogInfo,
            [AutomationIds.AboutCloseButton], AutomationIds.AboutCloseButton, AutomationIds.AboutCloseButton),
        new("Error", () => new ErrorDialog("Failed to run", "boom"), IconIds.DialogError,
            [AutomationIds.ErrorOkButton], AutomationIds.ErrorOkButton, AutomationIds.ErrorOkButton),
    }.ToDictionary(row => row.Name, StringComparer.Ordinal);

    public static TheoryData<string> Names => [.. Rows.Keys];

    private static ContributionRegistry Registry()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        registry.Freeze();
        return registry;
    }

    private static DialogShell ShellOf(Window window) => Assert.Single(window.GetVisualDescendants().OfType<DialogShell>());

    private static string[] FooterOrder(DialogShell shell) =>
        [.. shell.SortedActions.OfType<Button>()
            .OrderBy(button => button.TranslatePoint(default, shell)?.X ?? double.NaN)
            .Select(button => AutomationProperties.GetAutomationId(button) ?? "")];

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(Names))]
    public void ADialogIsHostedInTheShellWithItsTitleAndIcon(string name)
    {
        Row row = Rows[name];
        using var ui = HeadlessUi.Create();

        Window window = ui.Show(row.Create());

        DialogShell shell = ShellOf(window);
        Assert.False(string.IsNullOrWhiteSpace(shell.Title));
        Assert.Equal(window.Title, shell.Title);
        Assert.Equal(row.IconId, shell.IconId);
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(Names))]
    public void WindowsPutsTheDefaultButtonFirst(string name)
    {
        Row row = Rows[name];
        using var ui = HeadlessUi.Create();
        Window window = ui.Show(row.Create());
        DialogShell shell = ShellOf(window);

        shell.ButtonOrder = DialogButtonOrder.DefaultFirst;
        HeadlessDriverPump();

        string[] middle = [.. row.CancelFirst.Where(id => id != row.DefaultId && id != row.CancelId)];
        string[] last = [.. row.CancelFirst.Where(id => id == row.CancelId && id != row.DefaultId)];
        Assert.Equal([row.DefaultId, .. middle, .. last], FooterOrder(shell));
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(Names))]
    public void MacOsAndLinuxPutTheCancelButtonFirst(string name)
    {
        Row row = Rows[name];
        using var ui = HeadlessUi.Create();
        Window window = ui.Show(row.Create());
        DialogShell shell = ShellOf(window);

        shell.ButtonOrder = DialogButtonOrder.CancelFirst;
        HeadlessDriverPump();

        Assert.Equal(row.CancelFirst, FooterOrder(shell));
    }

    [Fact]
    public void ThePlatformPicksTheButtonOrder()
    {
        Assert.Same(DialogButtonOrder.DefaultFirst, DialogButtonOrder.For(isWindows: true));
        Assert.Same(DialogButtonOrder.CancelFirst, DialogButtonOrder.For(isWindows: false));
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(Names))]
    public async Task EnterRunsTheDefaultButton(string name)
    {
        Row row = Rows[name];
        using var ui = HeadlessUi.Create();
        Window window = ui.Show(row.Create());
        bool closed = false;
        window.Closed += (_, _) => closed = true;

        await ui.Driver.PressAsync("Enter", Cancel);

        Assert.True(closed, $"{name}: Enter closes through the default button");
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(Names))]
    public async Task EscapeRunsTheCancelButton(string name)
    {
        Row row = Rows[name];
        using var ui = HeadlessUi.Create();
        Window window = ui.Show(row.Create());
        bool closed = false;
        window.Closed += (_, _) => closed = true;

        await ui.Driver.PressAsync("Escape", Cancel);

        Assert.True(closed, $"{name}: Esc closes through the cancel button");
    }

    [AvaloniaTheory(Timeout = TestAppBuilder.Timeout)]
    [MemberData(nameof(Names))]
    public void TheWindowSizesToItsContentBetweenTheTwoWidthTokens(string name)
    {
        Row row = Rows[name];
        using var ui = HeadlessUi.Create();

        Window window = ui.Show(row.Create());

        Assert.NotEqual(SizeToContent.Manual, window.SizeToContent);
        Assert.InRange(window.ClientSize.Width, Token("Dialog.MinWidth"), Token("Dialog.MaxWidth"));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ALongMessageWrapsAtTheMaximumWidthInsteadOfGrowingTheWindow()
    {
        using var ui = HeadlessUi.Create();

        Window window = ui.Show(new ConfirmDialog("Delete it", string.Join(' ', Enumerable.Repeat("This cannot be undone.", 40)), "Delete"));

        Assert.True(window.ClientSize.Width <= Token("Dialog.MaxWidth"));
        Assert.True(window.ClientSize.Height > 150, "the wrapped message made the window taller");
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheTokensHoldTheDialogWidthsAndTheListHeight()
    {
        Assert.Equal(360, Token("Dialog.MinWidth"));
        Assert.Equal(640, Token("Dialog.MaxWidth"));
        Assert.Equal(360, Token("Dialog.ListMaxHeight"));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheIssuesListStopsAtTheListMaxHeight()
    {
        using var ui = HeadlessUi.Create();
        CodeDiagnostic[] issues = [.. Enumerable.Range(0, 200).Select(i => new CodeDiagnostic(CodeDiagnosticSeverity.Error, $"EXT{i}", "Extension failed to load", null, null, null, null, null))];

        Window window = ui.Show(new IssuesDialog("Extension issues", issues));

        ScrollViewer list = ListOf(window);
        Assert.True(list.Bounds.Height <= Token("Dialog.ListMaxHeight"), $"list is {list.Bounds.Height} high");
        Assert.True(list.Bounds.Height > 100);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheShortcutsListStopsAtTheListMaxHeight()
    {
        using var ui = HeadlessUi.Create();

        Window window = ui.Show(new KeyboardShortcutsDialog(new KeyboardShortcutsViewModel(Registry())));

        ScrollViewer list = ListOf(window);
        Assert.True(list.Bounds.Height <= Token("Dialog.ListMaxHeight"), $"list is {list.Bounds.Height} high");
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheTrustFolderListStopsAtTheListMaxHeight()
    {
        using var ui = HeadlessUi.Create();
        string[] folders = [.. Enumerable.Range(0, 100).Select(i => $"/projects/p/extensions/folder{i}")];

        Window window = ui.Show(new TrustDialog("/projects/p/P.csproj", folders));

        ScrollViewer list = ListOf(window);
        Assert.True(list.Bounds.Height <= Token("Dialog.ListMaxHeight"), $"list is {list.Bounds.Height} high");
    }

    private static ScrollViewer ListOf(Window window) =>
        Assert.Single(window.GetVisualDescendants().OfType<ScrollViewer>(), scroll => scroll.Content is ItemsControl);

    private static double Token(string key) =>
        Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out object? value) && value is double number ? number : double.NaN;

    private static void HeadlessDriverPump() => NetPrints.Editor.UITests.Driving.HeadlessDriver.Pump();
}
