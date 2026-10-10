using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Commands.KeyboardShortcuts;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Shell;

namespace NetPrints.Editor.Tests.Commands;

/// <summary>Help › Keyboard shortcuts and About (FR-036).</summary>
public class HelpMenuTests
{
    private static readonly string[] MenuOrder = ["File", "Edit", "View", "Go", "Build", "Help"];

    private static ContributionRegistry Registered(Action<IContributionRegistry>? extra = null)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        extra?.Invoke(registry);
        registry.Freeze();
        return registry;
    }

    [Fact]
    public void TheSheetListsEveryRegisteredCommandOnce()
    {
        ContributionRegistry registry = Registered();

        var sheet = new KeyboardShortcutsViewModel(registry);

        Assert.Equal(
            registry.Commands.Select(command => command.Id).Order(StringComparer.Ordinal),
            sheet.Groups.SelectMany(group => group.Rows).Select(row => row.CommandId).Order(StringComparer.Ordinal));
    }

    [Fact]
    public void TheSheetIsGroupedAsTheMenuBarAndCommandsWithoutAMenuComeLast()
    {
        var sheet = new KeyboardShortcutsViewModel(Registered());

        Assert.Equal([.. MenuOrder, KeyboardShortcutsViewModel.OtherTitle], sheet.Groups.Select(group => group.Title));
        ShortcutGroup edit = sheet.Groups.Single(group => group.Title == "Edit");
        Assert.Equal("netprints.command.undo", edit.Rows[0].CommandId);
        Assert.Contains(sheet.Groups.Single(group => group.Title == KeyboardShortcutsViewModel.OtherTitle).Rows, row => row.CommandId == "netprints.command.cancel");
    }

    [Fact]
    public void ARowShowsTheLabelAndEveryDefaultShortcut()
    {
        var sheet = new KeyboardShortcutsViewModel(Registered());

        ShortcutRow redo = sheet.Groups.SelectMany(group => group.Rows).Single(row => row.CommandId == "netprints.command.redo");
        ShortcutRow save = sheet.Groups.SelectMany(group => group.Rows).Single(row => row.CommandId == "netprints.command.save");
        ShortcutRow close = sheet.Groups.SelectMany(group => group.Rows).Single(row => row.CommandId == "netprints.command.closeProject");

        Assert.Equal("Redo", redo.Label);
        Assert.Equal("Ctrl+Y, Ctrl+Shift+Z", redo.Shortcuts);
        Assert.Equal("Ctrl+S", save.Shortcuts);
        Assert.Equal("", close.Shortcuts);
    }

    [Fact]
    public void ACommandFromAnotherContributionAppearsInItsMenu()
    {
        ContributionRegistry registry = Registered(extra => extra.AddCommand(new CommandDescriptor(
            "acme.command.hello", "Hello", new AboutCommandHandler(), DefaultGestures: ["Ctrl+H"], Menu: new MenuPlacement("Help", "acme", 0))));

        var sheet = new KeyboardShortcutsViewModel(registry);

        Assert.Contains(sheet.Groups.Single(group => group.Title == "Help").Rows, row => row is { CommandId: "acme.command.hello", Shortcuts: "Ctrl+H" });
    }

    [Fact]
    public void AboutShowsTheVersionAndTheProjectLinks()
    {
        var about = new AboutViewModel("1.2.3");

        Assert.Equal("1.2.3", about.Version);
        Assert.Contains(about.Links, link => link.Url == "https://github.com/danielmeza/netprints");
        Assert.Contains(about.Links, link => link.Url == "https://danielmeza.github.io/netprints/");
        Assert.Contains(about.Links, link => link.Url == "https://github.com/danielmeza/netprints/releases");
    }

    [Fact]
    public async Task TheHelpCommandsOpenTheirDialogsAndAreAlwaysEnabled()
    {
        var shell = new FakeShell();
        CommandContext context = shell.Context();

        Assert.True(new KeyboardShortcutsCommandHandler().CanExecute(context));
        Assert.True(new AboutCommandHandler().CanExecute(context));
        await new KeyboardShortcutsCommandHandler().ExecuteAsync(context, TestContext.Current.CancellationToken);
        await new AboutCommandHandler().ExecuteAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(["ShowKeyboardShortcuts", "ShowAbout"], shell.Project.Calls);
    }
}
