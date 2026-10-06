using System.ComponentModel;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// The composed shell: the frozen registry, <see cref="ShellViewModel"/>, the docking adapter, the command invoker and
/// the window. It closes the open documents when the project goes.
/// </summary>
internal sealed class ShellHost : IDisposable
{
    private readonly WindowCloseGuard closeGuard;

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private ShellHost(ShellProjectActions actions, IContributionRegistry registry, ShellViewModel shell, DockShellAdapter adapter, CommandInvoker invoker, ShellWindow window, ILogger logger)
    {
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
        var adapter = new DockShellAdapter(shell, actions, id => OpenDocument(id, shell, context));
        actions.Api = adapter;
        shell.Layout = adapter;

        var invoker = new CommandInvoker(registry, new ShellCommandContextProvider(shell, adapter),
            exception => context.Dispatcher.Post(() => context.Dialogs.ShowErrorAsync("The command failed", exception.ToString()).Forget(logger)));
        shell.AttachCommands(invoker);

        var host = new ShellHost(actions, registry, shell, adapter, invoker, new ShellWindow(), logger);
        shell.PropertyChanged += host.OnShellChanged;
        shell.AttachPanels(adapter, invoker, context);
        return host;
    }

    /// <summary>Disposes the shell state, then the open session.</summary>
    public void Dispose()
    {
        Shell.PropertyChanged -= OnShellChanged;
        closeGuard.Dispose();
        Shell.Dispose();
        Adapter.Dispose();
        Actions.Dispose();
    }

    private static DocumentViewModel? OpenDocument(DocumentId id, ShellViewModel shell, EditorContext context)
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
            foreach (DocumentId id in Adapter.OpenDocuments)
            {
                Adapter.CloseDocument(id);
            }
        }
    }
}
