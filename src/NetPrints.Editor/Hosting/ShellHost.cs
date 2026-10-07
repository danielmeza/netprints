using System.ComponentModel;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.StartPage;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// The composed shell: the frozen registry, <see cref="ShellViewModel"/>, the docking adapter, the command invoker and
/// the window. It closes the open documents when the project goes.
/// </summary>
internal sealed class ShellHost : IDisposable
{
    private readonly WindowCloseGuard closeGuard;
    private readonly ShellStatePersistence? persistence;
    private readonly StartPageController startPage;

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private ShellHost(ShellProjectActions actions, IContributionRegistry registry, ShellViewModel shell, DockShellAdapter adapter, CommandInvoker invoker, ShellWindow window, ILogger logger, ShellStatePersistence? persistence)
    {
        startPage = new StartPageController(adapter);
        this.persistence = persistence;
        Actions = actions;
        Registry = registry;
        Shell = shell;
        Adapter = adapter;
        Invoker = invoker;
        Window = window;
        window.DataContext = shell;
        closeGuard = new WindowCloseGuard(window, actions, logger);
    }

    /// <summary>Gets the project flows of the shell.</summary>
    public ShellProjectActions Actions { get; }

    /// <summary>Gets the frozen registry the shell was generated from.</summary>
    public IContributionRegistry Registry { get; }

    /// <summary>Gets the shell state.</summary>
    public ShellViewModel Shell { get; }

    /// <summary>Gets the shell API over the docking layout.</summary>
    public DockShellAdapter Adapter { get; }

    /// <summary>Gets the invoker of the registered commands.</summary>
    public CommandInvoker Invoker { get; }

    /// <summary>Gets the shell window.</summary>
    public ShellWindow Window { get; }

    /// <summary>Composes the shell.</summary>
    /// <param name="context">Host services shared across the editor.</param>
    /// <returns>The shell.</returns>
    public static ShellHost Create(EditorContext context)
    {
        ILogger logger = context.LoggerFactory.CreateLogger<ShellHost>();
        var registry = new ContributionRegistry(context.LoggerFactory.CreateLogger<ContributionRegistry>());
        BuiltInContributions.Register(registry);
        registry.Freeze();

        var shell = new ShellViewModel(registry, new NoServices(), TimeProvider.System, context.Dispatcher);
        shell.WindowStateService = context.WindowStateService;
        var actions = new ShellProjectActions(context, shell);
        var startPageServices = new StartPageServices().Add<IProjectActions>(actions).Add<IClipboardService>(context.Clipboard).Add<IFolderLauncher>(new ShellFolderLauncher()).Add(TimeProvider.System);
        if (context.StateStore is { } stateStore)
        {
            startPageServices.Add(stateStore);
        }

        if (context.Recent is { } recent)
        {
            startPageServices.Add(recent);
        }

        var adapter = new DockShellAdapter(shell, actions, id => OpenDocument(id, shell, context, startPageServices), context.LoggerFactory.CreateLogger<DockShellAdapter>());
        actions.Api = adapter;
        shell.Layout = adapter;
        shell.Projects = actions;

        var invoker = new CommandInvoker(registry, new ShellCommandContextProvider(shell, adapter),
            exception => context.Dispatcher.Post(() => context.Dialogs.ShowErrorAsync("The command failed", exception.ToString()).Forget(logger)));
        shell.AttachCommands(invoker);

        var window = new ShellWindow();
        ShellStatePersistence? persistence = context.StateStore is { } store ? new ShellStatePersistence(shell, adapter, window, store, TimeProvider.System, context.Dispatcher) : null;
        var host = new ShellHost(actions, registry, shell, adapter, invoker, window, logger, persistence);
        shell.PropertyChanged += host.OnShellChanged;
        shell.AttachPanels(adapter, invoker, context);
        host.SyncProjectState();
        return host;
    }

    /// <summary>Disposes the shell state, then the open session.</summary>
    public void Dispose()
    {
        Shell.PropertyChanged -= OnShellChanged;
        closeGuard.Dispose();
        persistence?.Dispose();
        Shell.Dispose();
        Adapter.Dispose();
        Actions.Dispose();
    }

    private static DocumentViewModel? OpenDocument(DocumentId id, ShellViewModel shell, EditorContext context, IServiceProvider startPageServices)
    {
        if (id.Kind == DocumentKind.StartPage)
        {
            return new StartPageViewModel(shell, startPageServices);
        }

        if (shell.Session is not { } session)
        {
            return null;
        }

        switch (id.Kind)
        {
            case DocumentKind.ProjectSettings:
                return new ProjectSettingsDocumentViewModel(session, context);
            case DocumentKind.Graph:
                return CommandTargets.GraphOf(session, id) is { } graph && CommandTargets.ClassOf(graph) is { } cls
                    ? GraphDocumentFactory.Open(id, graph, session.ContextFor(cls).Services, cls, session, shell.Commands)
                    : null;
            default:
                return null;
        }
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.Session))
        {
            persistence?.SaveSession();
            foreach (DocumentId id in Adapter.OpenDocuments)
            {
                Adapter.CloseDocument(id);
            }

            persistence?.RestoreSession(Shell.Session);
            SyncProjectState();
        }
    }

    private void SyncProjectState()
    {
        bool projectOpen = Shell.Session is not null;
        Adapter.SetPanelsSuspended(!projectOpen);
        startPage.Sync(projectOpen);
    }
}
