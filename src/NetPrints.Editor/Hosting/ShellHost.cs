using System.ComponentModel;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.Main;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// The composed shell: the frozen registry, <see cref="ShellViewModel"/>, the docking adapter, the command invoker and
/// the window. It follows the project service's session and closes the open documents when the project goes.
/// </summary>
internal sealed class ShellHost : IDisposable
{
    private readonly MainEditorViewModel projects;

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private ShellHost(MainEditorViewModel projects, IContributionRegistry registry, ShellViewModel shell, DockShellAdapter adapter, CommandInvoker invoker, ShellWindow window)
    {
        this.projects = projects;
        Registry = registry;
        Shell = shell;
        Adapter = adapter;
        Invoker = invoker;
        Window = window;
        window.DataContext = shell;
    }

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

    /// <summary>Composes the shell over a project service.</summary>
    /// <param name="context">Host services shared across the editor.</param>
    /// <param name="projects">The project service; it opens, creates and closes projects.</param>
    /// <returns>The shell; dispose it before <paramref name="projects"/>.</returns>
    public static ShellHost Create(EditorContext context, MainEditorViewModel projects)
    {
        ILogger logger = context.LoggerFactory.CreateLogger<ShellHost>();
        var registry = new ContributionRegistry(context.LoggerFactory.CreateLogger<ContributionRegistry>());
        BuiltInContributions.Register(registry);
        registry.Freeze();

        var shell = new ShellViewModel(registry, new NoServices(), TimeProvider.System, context.Dispatcher);
        var inspector = shell.FindPanel(PanelContributions.InspectorId)?.Content as InspectorPanelViewModel
            ?? throw new InvalidOperationException("The inspector panel is not registered.");
        var actions = new ShellProjectActions(projects, shell, inspector.EditorFor);
        var adapter = new DockShellAdapter(shell, actions, id => OpenDocument(id, shell, inspector, context));
        actions.Api = adapter;
        shell.Layout = adapter;

        var invoker = new CommandInvoker(registry, new ShellCommandContextProvider(shell, adapter),
            exception => context.Dispatcher.Post(() => context.Dialogs.ShowErrorAsync("The command failed", exception.ToString()).Forget(logger)));
        shell.AttachCommands(invoker);

        var host = new ShellHost(projects, registry, shell, adapter, invoker, new ShellWindow());
        shell.PropertyChanged += host.OnShellChanged;
        shell.AttachPanels(adapter, invoker, context);
        projects.PropertyChanged += host.OnProjectsChanged;
        projects.ShellNavigator = actions.Navigate;
        return host;
    }

    /// <summary>Stops following the project service and disposes the shell state.</summary>
    public void Dispose()
    {
        projects.PropertyChanged -= OnProjectsChanged;
        projects.ShellNavigator = null;
        Shell.PropertyChanged -= OnShellChanged;
        Shell.Dispose();
        Adapter.Dispose();
    }

    private static DocumentViewModel? OpenDocument(DocumentId id, ShellViewModel shell, InspectorPanelViewModel inspector, EditorContext context)
    {
        if (shell.Session is not { } session)
        {
            return null;
        }

        switch (id.Kind)
        {
            case DocumentKind.ProjectSettings:
                return new ProjectSettingsDocumentViewModel(session, context);
            case DocumentKind.Graph:
                return CommandTargets.GraphOf(session, id) is { } graph && (graph as ClassGraph ?? graph.Class) is { } cls
                    ? new GraphDocumentViewModel(id, new NodeGraphViewModel(graph, inspector.EditorFor(cls).Services), cls, session) { Invoker = shell.Commands }
                    : null;
            default:
                return null;
        }
    }

    private void OnProjectsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainEditorViewModel.Project))
        {
            Shell.Session = projects.Session;
        }
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.Session))
        {
            foreach (DocumentId id in Adapter.OpenDocuments)
            {
                Adapter.CloseDocument(id);
            }
        }
    }
}
