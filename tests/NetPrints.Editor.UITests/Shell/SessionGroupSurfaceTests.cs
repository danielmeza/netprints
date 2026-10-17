using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Commands.CommandPalette;
using NetPrints.Editor.Commands.KeyboardShortcuts;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Projects;
using NetPrints.Workspace;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>FR-032 and FR-033, US4 scenarios 1 and 2: adding and removing a session group updates every surface, and the palette and the shortcuts sheet name the owner.</summary>
public class SessionGroupSurfaceTests
{
    private const string GroupKey = "project-1";
    private const string ExtensionId = "acme.tools";
    private const string ExtensionName = "Acme Tools";
    private const string HelloId = "acme.tools.hello";
    private const string TileBody = "Acme tile body";
    private const string TemplateId = "acme.tools.template";
    private const string StatusId = "acme.tools.status";

    private sealed class HelloHandler : ICommandHandler
    {
        public bool CanExecute(ICommandContext context) => true;

        public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public bool CheckAccess() => true;
    }

    private static void AddGroup(ContributionRegistry registry) =>
        registry.AddSessionGroup(GroupKey, group =>
        {
            IEditorContributions editor = group.For(ExtensionId, ExtensionName);
            editor.AddCommand(new CommandDescriptor(
                HelloId,
                "Say hello",
                new HelloHandler(),
                DefaultGestures: [new CommandGesture(CommandModifiers.Ctrl | CommandModifiers.Alt, CommandKey.H)],
                Menu: new MenuPlacement("File", "acme", 0),
                CommandBarOrder: 99));
            editor.AddContextMenuItem(new ContextMenuItemDescriptor("acme.tools.menu", ContextMenuTarget.Connection, HelloId, "acme", 99));
            editor.AddDashboardTile(new DashboardTileDescriptor("acme.tools.tile", "Acme", 99, _ => TileBody));
            editor.AddStatusBarItem(new StatusBarItemDescriptor(StatusId, StatusBarAlignment.Right, 99, _ => "acme"));
            group.AddProjectTemplate(new ProjectTemplateDescriptor(TemplateId, "Acme app", "An Acme template", DefaultProjectProfile.ProfileId, ProjectOutputType.Library), ExtensionId);
        });

    private static bool Has(IEnumerable<MenuItem> items, string commandId) =>
        items.Any(item => Avalonia.Automation.AutomationProperties.GetAutomationId(item) == AutomationIds.MenuPrefix + commandId);

    private static bool HasKeyBinding(ShellWindow window) =>
        window.KeyBindings.Any(binding => binding.Gesture.Key == Key.H && binding.Gesture.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Alt));

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AddingAndRemovingASessionGroupUpdatesTheMenusTheCommandBarAndTheKeyBindings()
    {
        using var rig = SurfaceRig.Create();
        Assert.False(Has(rig.Open("File"), HelloId));
        Assert.Null(rig.Find(AutomationIds.CommandBarPrefix + HelloId));
        Assert.False(HasKeyBinding(rig.Window));

        AddGroup(rig.Registry);
        rig.Settle();

        Assert.True(Has(rig.Open("File"), HelloId));
        Assert.NotNull(rig.Find(AutomationIds.CommandBarPrefix + HelloId));
        Assert.True(HasKeyBinding(rig.Window));

        rig.Registry.RemoveSessionGroup(GroupKey);
        rig.Settle();

        Assert.False(Has(rig.Open("File"), HelloId));
        Assert.Null(rig.Find(AutomationIds.CommandBarPrefix + HelloId));
        Assert.False(HasKeyBinding(rig.Window));
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void AddingAndRemovingASessionGroupUpdatesTheContextMenusAndTheStatusBar()
    {
        using var rig = SurfaceRig.Create();
        Assert.DoesNotContain(rig.Invoker.ContextMenuCommands(ContextMenuTarget.Connection), command => command.Id == HelloId);
        Assert.DoesNotContain(rig.Shell.StatusBar.RightItems, item => item.Id == StatusId);

        AddGroup(rig.Registry);

        Assert.Contains(rig.Invoker.ContextMenuCommands(ContextMenuTarget.Connection), command => command.Id == HelloId);
        Assert.Contains(rig.Shell.StatusBar.RightItems, item => item.Id == StatusId);

        rig.Registry.RemoveSessionGroup(GroupKey);

        Assert.DoesNotContain(rig.Invoker.ContextMenuCommands(ContextMenuTarget.Connection), command => command.Id == HelloId);
        Assert.DoesNotContain(rig.Shell.StatusBar.RightItems, item => item.Id == StatusId);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void ThePaletteAndTheShortcutsSheetNameTheOwnerOfASessionGroupCommand()
    {
        using var rig = SurfaceRig.Create();
        AddGroup(rig.Registry);
        var palette = new CommandPaletteViewModel(rig.Registry, rig.Invoker);
        var sheet = new KeyboardShortcutsViewModel(rig.Registry);
        using var paletteUi = HeadlessUi.Create();
        using var sheetUi = HeadlessUi.Create();

        paletteUi.Show(new CommandPaletteDialog(palette));
        KeyboardShortcutsDialog sheetWindow = sheetUi.Show(new KeyboardShortcutsDialog(sheet));

        HeadlessDriver.Pump();
        Assert.Equal(ExtensionName, palette.Items.Single(item => item.Command.Id == HelloId).Owner);
        Assert.Equal(ExtensionName, sheet.Groups.SelectMany(group => group.Rows).Single(row => row.CommandId == HelloId).Owner);
        palette.Query = "hello";
        HeadlessDriver.Pump();
        Assert.Equal([ExtensionName], Texts(paletteUi, AutomationIds.PaletteOwner));
        palette.Query = "";
        Assert.Equal([ExtensionName], sheetWindow.GetVisualDescendants().OfType<TextBlock>().Where(text => Avalonia.Automation.AutomationProperties.GetAutomationId(text) == AutomationIds.ShortcutsOwner && text.IsVisible).Select(text => text.Text));
        Assert.All(palette.Items.Where(item => item.Command.Id != HelloId), item => Assert.Equal("", item.Owner));
        Assert.All(sheet.Groups.SelectMany(group => group.Rows).Where(row => row.CommandId != HelloId), row => Assert.Equal("", row.Owner));

        rig.Registry.RemoveSessionGroup(GroupKey);

        Assert.DoesNotContain(new CommandPaletteViewModel(rig.Registry, rig.Invoker).Items, item => item.Command.Id == HelloId);
        Assert.DoesNotContain(new KeyboardShortcutsViewModel(rig.Registry).Groups.SelectMany(group => group.Rows), row => row.CommandId == HelloId);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public void TheStartPageAndTheTemplatesFollowTheSessionGroup()
    {
        using var rig = SurfaceRig.Create();
        var projects = new MsBuildProjectSystem(new ProjectSystemOptions([], "1.0.0"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);
        var templates = new ProjectTemplateService(() => rig.Registry.ProjectTemplates, _ => DefaultProjectProfile.Instance, projects);
        using var page = new StartPageViewModel(rig.Shell, new StartPageContext(new NoProjectActions(), PanelFactoryContext.Detached));
        using var ui = HeadlessUi.Create();
        Window window = ui.Show(new Window { Width = 1600, Height = 1000, Content = new StartPageView { DataContext = page } });
        object[] builtIn = [.. page.Tiles];
        Assert.DoesNotContain(TileBody, page.Tiles);
        Assert.DoesNotContain(templates.Templates, template => template.Id == TemplateId);

        AddGroup(rig.Registry);
        HeadlessDriver.Pump();

        Assert.Equal(TileBody, page.Tiles[^1]);
        Assert.Equal(builtIn, page.Tiles.Take(builtIn.Length));
        Assert.Contains(page.Others, tile => Equals(tile, TileBody));
        Assert.Contains(window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == TileBody);
        Assert.Contains(templates.Templates, template => template.Id == TemplateId);

        rig.Registry.RemoveSessionGroup(GroupKey);
        HeadlessDriver.Pump();

        Assert.DoesNotContain(TileBody, page.Tiles);
        Assert.Equal(builtIn, page.Tiles);
        Assert.DoesNotContain(templates.Templates, template => template.Id == TemplateId);
    }

    private static string[] Texts(HeadlessUi ui, string automationId) =>
        [.. ui.Tree.FindControls(new AutomationQuery(automationId)).Select(pair => Assert.IsType<TextBlock>(pair.Control)).Select(text => text.Text ?? "")];
}
