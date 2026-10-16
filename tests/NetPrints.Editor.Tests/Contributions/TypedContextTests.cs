using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;
using DocumentId = NetPrints.Editor.Shell.DocumentId;

namespace NetPrints.Editor.Tests.Contributions;

/// <summary>FR-022, NPXE0005: handlers and factories receive typed contexts whose members are all there in the editor host.</summary>
public sealed class TypedContextTests : Commands.SessionCommandTests
{
    private const string ExtensionCommandId = "testext.command.hello";

    private sealed class RecordingHandler : ICommandHandler
    {
        public ICommandContext? Received { get; private set; }

        public bool CanExecute(ICommandContext context) => true;

        public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken)
        {
            Received = context;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedContexts(Func<CommandContext> create) : ICommandContextProvider
    {
        public event EventHandler? CommandStatesChanged
        {
            add { }
            remove { }
        }

        public CommandContext Create(object? parameter = null, CommandScope scope = CommandScope.Global) => create();
    }

    private (CommandContext Context, NodeGraphViewModel Graph) HostContext(ProjectSessionViewModel session)
    {
        var classContext = Track(new ClassContext(new ClassGraph { Name = "C", Namespace = "N" }, Editor.Context, new UndoRedoStack()));
        var graph = Track(new NodeGraphViewModel(classContext.CreateMethod(), classContext.Services));
        var provider = new ShellCommandContextProvider(NewShell(session, graph), Shell, Editor.Context);
        return (provider.Create("parameter", CommandScope.Graph), graph);
    }

    private ShellViewModel NewShell(ProjectSessionViewModel session, NodeGraphViewModel graph)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.Freeze();
        ShellViewModel shell = Track(new ShellViewModel(registry, PanelFactoryContext.Detached, TimeProvider.System, new InlineDispatcher()));
        shell.Session = session;
        var document = new GraphDocumentViewModel(DocumentId.Graph("C.cs", "method:1"), graph, new ClassGraph { Name = "C", Namespace = "N" }, session);
        shell.AddDocument(document);
        shell.ActiveDocument = document;
        return shell;
    }

    [Fact]
    public async Task EveryMemberOfTheCommandContextIsNonNullInTheEditorHost()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        await Editor.Reflection.ReloadAsync(session.Project, TestContext.Current.CancellationToken);
        ICommandContext context = HostContext(session).Context;

        Assert.NotNull(context.Shell);
        Assert.NotNull(context.Session);
        Assert.NotNull(context.ActiveGraph);
        Assert.NotNull(context.Selection);
        Assert.NotNull(context.Parameter);
        Assert.Equal(CommandScope.Graph, context.Scope);
        Assert.NotNull(context.Dialogs);
        Assert.NotNull(context.Output);
        Assert.NotNull(context.Services);

        IProjectSession projectSession = Assert.IsAssignableFrom<IProjectSession>(context.Session);
        Assert.NotNull(projectSession.Project);
        Assert.NotNull(projectSession.Target);
        Assert.NotNull(projectSession.Reflection);
        Assert.NotNull(projectSession.ActiveGraph);
        Assert.NotNull(projectSession.Services);

        IGraphEditor graph = Assert.IsAssignableFrom<IGraphEditor>(context.ActiveGraph);
        Assert.NotNull(graph.Graph);
        Assert.NotNull(graph.SelectedNodes);
        Assert.NotNull(graph.Undo);
    }

    [Fact]
    public async Task EveryMemberOfThePanelStartPageStatusBarSettingsPageAndDialogContextsIsNonNull()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        ICommandContext command = HostContext(session).Context;

        var panel = new PanelFactoryContext(new EditorShellFacade(() => Shell), (EditorDialogService)command.Dialogs, (EditorOutput)command.Output, command.Services);
        IPanelContext panelContext = panel;
        Assert.NotNull(panelContext.Shell);
        Assert.NotNull(panelContext.Dialogs);
        Assert.NotNull(panelContext.Output);
        Assert.NotNull(panelContext.Services);

        IStartPageContext startPage = new StartPageContext(new FakeProjectActions(), panel);
        Assert.NotNull(startPage.Shell);
        Assert.NotNull(startPage.Dialogs);
        Assert.NotNull(startPage.Output);
        Assert.NotNull(startPage.Services);

        IStatusBarContext statusBar = new EditorContexts(command);
        Assert.NotNull(statusBar.Shell);
        Assert.NotNull(statusBar.Session);
        Assert.NotNull(statusBar.ActiveGraph);
        Assert.NotNull(statusBar.Services);

        ISettingsPageContext settings = new EditorContexts(command);
        Assert.NotNull(settings.Shell);
        Assert.NotNull(settings.Dialogs);
        Assert.NotNull(settings.Services);

        IDialogContext dialog = new EditorContexts(command);
        Assert.NotNull(dialog.Shell);
        Assert.NotNull(dialog.Session);
        Assert.NotNull(dialog.Dialogs);
        Assert.NotNull(dialog.Services);
    }

    [Fact]
    public async Task AContextMenuItemWhoseAppliesToIsFalseIsHiddenNotDisabled()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        CommandContext context = HostContext(session).Context;
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.AddCommand(new CommandDescriptor(ExtensionCommandId, "Hello", new RecordingHandler()));
        registry.AddContextMenuItem(new ContextMenuItemDescriptor("testext.menu.shown", ContextMenuTarget.Connection, ExtensionCommandId, "testext", 0, _ => true));
        registry.AddContextMenuItem(new ContextMenuItemDescriptor("testext.menu.hidden", ContextMenuTarget.TreeClass, ExtensionCommandId, "testext", 0, _ => false));
        registry.Freeze();
        var invoker = new CommandInvoker(registry, new FixedContexts(() => context), _ => { });

        Assert.Single(invoker.ContextMenuCommands(ContextMenuTarget.Connection, context));
        Assert.Empty(invoker.ContextMenuCommands(ContextMenuTarget.TreeClass, context));
        Assert.Single(invoker.ContextMenuCommands(ContextMenuTarget.TreeClass));
    }

    [Fact]
    public async Task AHandlerIsStatelessAndReceivesTheTypedContext()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        CommandContext context = HostContext(session).Context;
        var handler = new RecordingHandler();
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        registry.AddCommand(new CommandDescriptor(ExtensionCommandId, "Hello", handler));
        registry.Freeze();
        var invoker = new CommandInvoker(registry, new FixedContexts(() => context), _ => { });

        Assert.True(invoker.TryRun(registry.Commands.Single()));
        await Task.Yield();

        Assert.Same(context, handler.Received);
        Assert.IsAssignableFrom<ICommandContext>(handler.Received);
    }
}
