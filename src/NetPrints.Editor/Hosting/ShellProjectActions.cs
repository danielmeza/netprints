using System.ComponentModel;
using Microsoft.Extensions.Logging;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Graph;
using NetPrints.Editor.References;
using NetPrints.Editor.Shell;
using NetPrints.Reflection;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// The project flows of the shell window: open and create go to <see cref="ProjectLoader"/>, close drops the shell's
/// session, the member edits act on the class's one editor and open the new graph as a document. It also answers the
/// host channel: a types-changed message reloads reflection, a focus-document message opens the document.
/// </summary>
internal sealed class ShellProjectActions : IProjectActions, IDisposable
{
    private const string RemoveClassTitle = "Remove class";
    private const string RemoveClassConfirm = "Remove";
    private const string StopRunningTitle = "Stop running program?";
    private const string StopRunningMessage = "The program is still running. Stop it and exit?";
    private const string StopRunningConfirm = "Stop and exit";

    private readonly EditorContext context;
    private readonly ShellViewModel shell;
    private readonly HostChannelBridge hostChannelBridge;
    private ProjectSessionViewModel? followedSession;
    private Task<bool>? exitConfirmation;
    private bool exitConfirmed;

    /// <summary>Creates the actions and starts listening to the host channel.</summary>
    /// <param name="context">Host services shared across the editor.</param>
    /// <param name="shell">The shell state.</param>
    public ShellProjectActions(EditorContext context, ShellViewModel shell)
    {
        this.context = context;
        this.shell = shell;
        Loader = new ProjectLoader(context, shell);
        hostChannelBridge = new HostChannelBridge(context.HostChannel, context.Dispatcher, Loader.ReloadReflectionAsync, FocusDocument,
            context.LoggerFactory.CreateLogger<HostChannelBridge>());
        shell.PropertyChanged += OnShellChanged;
        Follow(shell.Session);
    }

    /// <summary>Gets the loader of projects.</summary>
    public ProjectLoader Loader { get; }

    /// <summary>Gets or sets the shell API; set once the layout exists.</summary>
    public IShell? Api { get; set; }

    /// <inheritdoc/>
    public async Task<bool> ConfirmUnloadAsync(CancellationToken cancellationToken)
    {
        if (shell.Session is not { } session || !session.Unsaved.HasUnsavedFiles)
        {
            return true;
        }

        UnloadChoice choice = await context.Dialogs.ConfirmUnsavedAsync(session.Unsaved.UnsavedFiles).ConfigureAwait(true);
        return choice switch
        {
            UnloadChoice.Save => await session.SaveAllAsync().ConfigureAwait(true),
            UnloadChoice.Discard => true,
            _ => false,
        };
    }

    /// <summary>Gets whether leaving now needs <see cref="ConfirmExitAsync"/>: a build runs, the program runs or files are unsaved, and no exit was confirmed yet.</summary>
    public bool ExitNeedsConfirmation =>
        !exitConfirmed && shell.Session is { } session && (session.IsBuilding || session.IsRunning || session.Unsaved.HasUnsavedFiles);

    /// <summary>
    /// Asks whether the application may exit: waits for a build in flight, asks to stop a running program, then asks
    /// <see cref="ConfirmUnloadAsync"/>. A request made while one is pending joins it, and once the exit was confirmed
    /// every later request passes, so the window close and the shutdown request that follows it ask once.
    /// </summary>
    /// <param name="cancellationToken">Cancels the prompts.</param>
    /// <returns><see langword="true"/> to go on, <see langword="false"/> when the user kept the project or the program running.</returns>
    public Task<bool> ConfirmExitAsync(CancellationToken cancellationToken)
    {
        if (exitConfirmed)
        {
            return Task.FromResult(true);
        }

        if (exitConfirmation is { IsCompleted: false } pending)
        {
            return pending;
        }

        exitConfirmation = ConfirmExitCoreAsync(cancellationToken);
        return exitConfirmation;
    }

    private async Task<bool> ConfirmExitCoreAsync(CancellationToken cancellationToken)
    {
        if (shell.Session is { } session)
        {
            await session.WaitForBuildAsync().ConfigureAwait(true);
            if (session.IsRunning)
            {
                if (!await context.Dialogs.ConfirmAsync(StopRunningTitle, StopRunningMessage, StopRunningConfirm).ConfigureAwait(true))
                {
                    return false;
                }

                session.Stop();
            }
        }

        exitConfirmed = await ConfirmUnloadAsync(cancellationToken).ConfigureAwait(true);
        return exitConfirmed;
    }

    /// <inheritdoc/>
    public Task OpenProjectAsync(string? path, CancellationToken cancellationToken) => Loader.OpenProjectAsync(path);

    /// <inheritdoc/>
    public Task NewProjectAsync(CancellationToken cancellationToken) => Loader.CreateProjectAsync();

    /// <inheritdoc/>
    public Task CloseProjectAsync(CancellationToken cancellationToken)
    {
        Loader.CloseProject();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task ExitAsync(CancellationToken cancellationToken)
    {
        context.Windows.CloseMainWindow();
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public void ShowProjectSettings() => Api?.OpenDocument(DocumentId.ProjectSettings);

    /// <inheritdoc/>
    public async Task ShowReferencesAsync(CancellationToken cancellationToken)
    {
        if (shell.Session?.Project is not { } project)
        {
            return;
        }

        using var references = new ReferenceListViewModel(project, context);
        await context.Dialogs.ShowReferencesAsync(references).ConfigureAwait(true);
    }

    /// <inheritdoc/>
    public async Task NewClassAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (shell.Session?.Project is { } project)
            {
                project.CreateNewClass(ResolveProfile(project));
            }
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to create class", ex.ToString()).ConfigureAwait(true);
        }
    }

    /// <inheritdoc/>
    public async Task AddExistingClassAsync(CancellationToken cancellationToken)
    {
        if (shell.Session?.Project is not { } project)
        {
            return;
        }

        string? path = await context.FilePicker.OpenFileAsync("Add Existing Class", [FileFilter.ClassFiles]).ConfigureAwait(true);
        if (path is null)
        {
            return;
        }

        try
        {
            (_, IReadOnlyList<Serialization.DocumentIssue> issues) = await context.Persistence.AddGraphAsync(project, path, CancellationToken.None).ConfigureAwait(true);
            if (issues.Count > 0)
            {
                await context.Dialogs.ShowErrorAsync("Class added with issues",
                    string.Join("\n\n", issues.Select(issue => $"{issue.Code}: {issue.Message}"))).ConfigureAwait(true);
            }
        }
        catch (Exception ex)
        {
            await context.Dialogs.ShowErrorAsync("Failed to load existing class",
                $"Failed to load existing class at path {path}:\n\n{ex}").ConfigureAwait(true);
        }
    }

    /// <inheritdoc/>
    public void ShowClassSettings(ClassGraph cls) => Inspect(cls);

    /// <inheritdoc/>
    public void AddMethod(ClassGraph cls) => AddGraph(cls, classContext => classContext.CreateMethod());

    /// <inheritdoc/>
    public void AddConstructor(ClassGraph cls) => AddGraph(cls, classContext => classContext.CreateConstructor());

    /// <inheritdoc/>
    public void AddEventGraph(ClassGraph cls) => AddGraph(cls, classContext => classContext.CreateEventGraph());

    /// <inheritdoc/>
    public async Task OverrideMethodAsync(ClassGraph cls, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(cls);
        if (!context.Reflection.IsLoaded)
        {
            await context.Dialogs.ShowErrorAsync("Override method", "The project's types are still loading.").ConfigureAwait(true);
            return;
        }

        IReflectionProvider provider = context.Reflection.Provider;
        List<MethodSpecifier> overridable = cls.AllBaseTypes.SelectMany(provider.GetOverridableMethodsForType).ToList();
        if (overridable.Count == 0)
        {
            await context.Dialogs.ShowErrorAsync("Override method", $"{cls.Name} has no base method to override.").ConfigureAwait(true);
            return;
        }

        MethodSpecifier? chosen = await context.Dialogs.SelectMethodAsync(overridable).ConfigureAwait(true);
        if (chosen is not null)
        {
            AddGraph(cls, classContext => classContext.CreateOverride(chosen));
        }
    }

    /// <inheritdoc/>
    public void AddVariable(ClassGraph cls)
    {
        shell.Session?.ContextFor(cls).CreateVariable();
        if (cls.Variables.LastOrDefault() is { } variable)
        {
            Inspect(variable);
        }
    }

    /// <inheritdoc/>
    public void RenameItem(object item)
    {
        if (item is ClassGraph or MethodGraph or ConstructorGraph or Variable or EventGraph)
        {
            Inspect(item);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteItemAsync(object item, CancellationToken cancellationToken)
    {
        switch (item)
        {
            case ClassGraph cls:
                if (!await context.Dialogs.ConfirmAsync(RemoveClassTitle, $"Remove class '{cls.Name}' and close its graphs? This cannot be undone.", RemoveClassConfirm).ConfigureAwait(true))
                {
                    break;
                }

                CloseDocumentsOf(cls);
                shell.Session?.Project.Classes.Remove(cls);
                break;
            case MethodGraph { Class: { } owner } method when shell.Session?.ContextFor(owner) is { } classContext:
                CloseGraph(method);
                classContext.RemoveMethod(method);
                break;
            case ConstructorGraph { Class: { } owner } constructor when shell.Session?.ContextFor(owner) is { } classContext:
                CloseGraph(constructor);
                classContext.RemoveMethod(constructor);
                break;
            case Variable { Class: { } owner } variable
                when shell.Session?.ContextFor(owner).Variables.FirstOrDefault(entry => ReferenceEquals(entry.Variable, variable)) is { } variableEntry:
                variableEntry.RemoveCommand.Execute(null);
                break;
            case EventGraph { Class: { } owner } eventGraph when shell.Session?.ContextFor(owner) is { } classContext:
                CloseGraph(eventGraph);
                classContext.RemoveEventGraph(eventGraph);
                break;
        }
    }

    /// <summary>Opens the class's graph, or the graph the given node lives in, and reveals the node.</summary>
    /// <param name="cls">The class.</param>
    /// <param name="nodeId">The node to reveal, or null to open the class graph.</param>
    public void Navigate(ClassGraph cls, string? nodeId)
    {
        if (shell.Session is not { } session || Api is not { } api)
        {
            return;
        }

        NodeGraph target = nodeId is not null && GraphKeys.ForNode(cls, nodeId) is { } key && GraphKeys.Resolve(cls, key) is { } graph ? graph : cls;
        if (CommandTargets.GraphDocumentOf(session, target) is not { } id)
        {
            return;
        }

        api.OpenDocument(id);
        if (nodeId is not null && shell.FindDocument(id) is GraphDocumentViewModel document)
        {
            document.Graph.RevealNode(nodeId);
        }
    }

    /// <summary>Stops listening to the host channel and disposes the open session.</summary>
    public void Dispose()
    {
        shell.PropertyChanged -= OnShellChanged;
        Follow(null);
        hostChannelBridge.Dispose();
        Loader.Dispose();
    }

    /// <summary>
    /// Opens the class whose graph file is <paramref name="path"/> (project-relative or absolute) and, when
    /// <paramref name="nodeId"/> resolves to one of its graphs, reveals that node (R2-21): the host channel's
    /// <c>focusDocument</c>.
    /// </summary>
    private bool FocusDocument(string path, string? nodeId)
    {
        if (shell.Session?.Project is not { } project)
        {
            return false;
        }

        string projectDirectory = Path.GetDirectoryName(project.Path) ?? "";
        string fullPath = Path.GetFullPath(path, projectDirectory);
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        ClassGraph? cls = project.Classes.FirstOrDefault(c => string.Equals(Path.GetFullPath(project.GetGraphFilePath(c)), fullPath, comparison));
        if (cls is null)
        {
            return false;
        }

        Navigate(cls, nodeId);
        return true;
    }

    // The profile the project's NetPrintsProfile names, or the default one when no loaded extension provides it
    // (the NPD005 warning was shown on load, extension-points.md §5).
    private IProjectProfile ResolveProfile(Project project) =>
        project.Snapshot is { } snapshot && context.Extensions.Current.FindProfile(snapshot.ProfileId) is { } profile
            ? profile
            : DefaultProjectProfile.Instance;

    private void Inspect(object item)
    {
        shell.TreeSelection = item;
        Api?.ShowPanel(PanelContributions.InspectorId);
    }

    private void AddGraph(ClassGraph cls, Func<ClassContext, NodeGraph?> create)
    {
        if (shell.Session is { } session && create(session.ContextFor(cls)) is { } graph && CommandTargets.GraphDocumentOf(session, graph) is { } id)
        {
            Api?.OpenDocument(id);
            shell.TreeSelection = graph;
        }
    }

    private void OnShellChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.Session))
        {
            Follow(shell.Session);
        }
    }

    private void Follow(ProjectSessionViewModel? session)
    {
        if (ReferenceEquals(session, followedSession))
        {
            return;
        }

        if (followedSession is not null)
        {
            followedSession.GraphOpenRequested -= OnGraphOpenRequested;
            followedSession.MembersChanged -= OnMembersChanged;
        }

        followedSession = session;
        if (session is not null)
        {
            session.GraphOpenRequested += OnGraphOpenRequested;
            session.MembersChanged += OnMembersChanged;
        }
    }

    private void OnGraphOpenRequested(object? sender, NodeGraph graph)
    {
        if (followedSession is { } session && CommandTargets.GraphDocumentOf(session, graph) is { } id)
        {
            Api?.OpenDocument(id);
        }
    }

    // Undo or redo of a creation removes a graph: its tab closes (redo does not reopen it).
    private void OnMembersChanged(object? sender, EventArgs e)
    {
        if (followedSession is not { } session || Api is not { } api)
        {
            return;
        }

        foreach (DocumentId id in api.OpenDocuments.Where(id => id.Kind == DocumentKind.Graph && CommandTargets.GraphOf(session, id) is null).ToList())
        {
            api.CloseDocument(id);
        }
    }

    private void CloseGraph(NodeGraph graph)
    {
        if (shell.Session is { } session && CommandTargets.GraphDocumentOf(session, graph) is { } id)
        {
            Api?.CloseDocument(id);
        }
    }

    private void CloseDocumentsOf(ClassGraph cls)
    {
        if (shell.Session is not { } session || Api is not { } api)
        {
            return;
        }

        string classPath = session.ClassPathOf(cls);
        foreach (DocumentId id in api.OpenDocuments.Where(id => id.ClassPath == classPath).ToList())
        {
            api.CloseDocument(id);
        }
    }
}
