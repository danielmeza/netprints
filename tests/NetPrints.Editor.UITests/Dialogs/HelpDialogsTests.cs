using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Commands.KeyboardShortcuts;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Dialogs;

/// <summary>The Help menu's dialogs show the registry's commands and the version (FR-036).</summary>
public class HelpDialogsTests
{
    private static IReadOnlyList<Control> Find(HeadlessUi ui, string automationId) =>
        [.. ui.Tree.FindControls(new AutomationQuery(automationId)).Select(pair => pair.Control)];

    private static string[] TextsOf(IReadOnlyList<Control> rows) =>
        [.. rows.Select(row => Avalonia.Automation.AutomationProperties.GetName(row) ?? "")];

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheShortcutsSheetShowsOneRowPerRegisteredCommandAndEachGroupTitle()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        registry.Freeze();
        var sheet = new KeyboardShortcutsViewModel(registry);
        using var ui = HeadlessUi.Create();

        ui.Show(new KeyboardShortcutsDialog(sheet));

        Assert.Equal(registry.Commands.Count, Find(ui, AutomationIds.ShortcutsRow).Count);
        Assert.Equal(sheet.Groups.Count, Find(ui, AutomationIds.ShortcutsGroupTitle).Count);
        Assert.Contains("Undo", TextsOf(Find(ui, AutomationIds.ShortcutsRow)));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AboutShowsTheVersionAndOneRowPerLink()
    {
        var about = new AboutViewModel("1.2.3");
        using var ui = HeadlessUi.Create();

        ui.Show(new AboutDialog(about));

        Assert.Equal("Version 1.2.3", Assert.IsType<TextBlock>(Assert.Single(Find(ui, AutomationIds.AboutVersion))).Text);
        Assert.Equal(about.Links.Count, Find(ui, AutomationIds.AboutLinkRow).Count);
    }
}
