using System.ComponentModel;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Icons;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.Navigation;
using NetPrints.Editor.Settings;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.StartPage;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// The composed shell: the frozen registry, <see cref="ShellViewModel"/>, the docking adapter, the command invoker and
/// the window. It closes the open documents when the project goes.
/// </summary>
[Singleton<ShellHost>(Factory = "Create")]
internal sealed class ShellHost : IDisposable
{
    private readonly WindowCloseGuard closeGuard;
    private readonly ShellStatePersistence? persistence;
    private readonly StartPageController startPage;
    private EventHandler<ContributionSnapshot>? followResources;

    private sealed class ComposedShell
    {
        public IShell? Api { get; set; }
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
    /// <param name="registry">The frozen registry the shell is generated from.</param>
    /// <returns>The shell.</returns>
    public static ShellHost Create(EditorContext context, IContributionRegistry registry)
    {
        ILogger logger = context.LoggerFactory.CreateLogger<ShellHost>();
        ILogger<IconRegistry> iconLogger = context.LoggerFactory.CreateLogger<IconRegistry>();
        EventHandler<ContributionSnapshot>? followResources = null;
        if (global::Avalonia.Application.Current is { } application)
        {
            SyncViewResources(application, registry, logger);
            followResources = (_, snapshot) => SyncViewResources(application, snapshot, logger);
            registry.Changed += followResources;
            IconRegistry.Shared = IconRegistry.ForApplication(application, iconLogger);
        }
        else
        {
            IconRegistry.Shared = new IconRegistry(iconLogger);
        }

        var composed = new ComposedShell();
        var panelContext = new PanelFactoryContext(
            new EditorShellFacade(() => composed.Api ?? throw new InvalidOperationException("The shell is not composed yet.")),
            new EditorDialogService(context.Dialogs),
            new EditorOutput(context.RunState),
            EmptyServiceProvider.Instance);
        var shell = new ShellViewModel(registry, panelContext, TimeProvider.System, context.Dispatcher, context);
        shell.WindowStateService = context.WindowStateService;
        var actions = new ShellProjectActions(context, shell);
        var startPageContext = new StartPageContext(actions, panelContext, context.Recent, context.StateStore, context.Clipboard, new ShellFolderLauncher());

        var adapter = new DockShellAdapter(shell, actions, id => OpenDocument(id, shell, context, startPageContext, registry), context.LoggerFactory.CreateLogger<DockShellAdapter>());
        composed.Api = adapter;
        actions.Api = adapter;
        shell.Layout = adapter;
        shell.Projects = actions;

        var invoker = new CommandInvoker(registry, new ShellCommandContextProvider(shell, adapter, context),
            exception => context.Dispatcher.Post(() => context.Dialogs.ShowErrorAsync("The command failed", exception.ToString()).Forget(logger)));
        shell.AttachCommands(invoker);

        var window = new ShellWindow();
        ShellStatePersistence? persistence = context.StateStore is { } store ? new ShellStatePersistence(shell, adapter, window, store, TimeProvider.System, context.Dispatcher) : null;
        var host = new ShellHost(actions, registry, shell, adapter, invoker, window, logger, persistence) { followResources = followResources };
        shell.PropertyChanged += host.OnShellChanged;
        shell.AttachPanels(adapter, invoker, context);
        host.SyncProjectState();
        return host;
    }

    /// <summary>Disposes the shell state, then the open session.</summary>
    public void Dispose()
    {
        Shell.PropertyChanged -= OnShellChanged;
        if (followResources is not null)
        {
            Registry.Changed -= followResources;
        }

        closeGuard.Dispose();
        persistence?.Dispose();
        Shell.Dispose();
        Adapter.Dispose();
        Actions.Dispose();
    }

    private static void SyncViewResources(global::Avalonia.Application application, IContributionSnapshot snapshot, ILogger logger)
    {
        foreach (ViewResourceFailure failure in ViewResourceLoader.Sync(application, snapshot.ViewResources, (declaration, exception) => Log.ViewTemplateFailed(logger, exception, declaration.Owner, declaration.Uri)))
        {
            Log.ViewResourceIgnored(logger, failure.Declaration.Owner, failure.Declaration.Uri, failure.Reason);
        }
    }

    private static DocumentViewModel? OpenDocument(DocumentId id, ShellViewModel shell, EditorContext context, StartPageContext startPageContext, IContributionRegistry contributions)
    {
        if (id.Kind == DocumentKind.StartPage)
        {
            return new StartPageViewModel(shell, startPageContext);
        }

        if (id.Kind == DocumentKind.Settings)
        {
            return new SettingsDocumentViewModel(
                context.Settings,
                contributions,
                context.Extensions,
                context.Dispatcher,
                page => new EditorContexts((shell.Commands ?? throw new InvalidOperationException("The shell has no commands.")).CreateContext() with { Owner = contributions.OwnerOf(page.Id), Contributions = contributions }),
                context.FilePicker,
                cancellationToken => shell.Projects?.ExitAsync(cancellationToken) ?? Task.CompletedTask);
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
                    ? GraphDocumentFactory.Open(id, graph, session.ContextFor(cls).Services, cls, session, shell.Commands,
                        graphModel => shell.Layout is IShell api ? new BreadcrumbsViewModel(session.Project, graphModel, shell, BreadcrumbsViewModel.RevealInTree(shell, api)) : null)
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
