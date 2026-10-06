using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Shell;

/// <summary>
/// The state of one shell window: the open session, its documents, the panels from the registry and the status bar.
/// Raises <see cref="CommandStatesChanged"/> when the session is replaced, the session's own command state changes or
/// the active document changes. Holds no docking types: the docking adapter keeps <see cref="Documents"/> and
/// <see cref="ActiveDocument"/> in step with the layout.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject, ICommandStateSource, IDisposable
{

    private readonly IContributionRegistry registry;
    private readonly List<PanelViewModel> panels;
    private Project? followedProject;
    private ProjectSessionViewModel? followedSession;
    private DocumentViewModel? followedDocument;

    /// <summary>Creates the shell state; every registered panel's view model is created now.</summary>
    /// <param name="registry">The registry the panels come from.</param>
    /// <param name="services">The services panel view models are created with.</param>
    /// <param name="timeProvider">The clock of the status message expiry.</param>
    /// <param name="dispatcher">Brings the status message expiry to the UI thread.</param>
    public ShellViewModel(IContributionRegistry registry, IServiceProvider services, TimeProvider timeProvider, IUiDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(dispatcher);
        this.registry = registry;
        panels =
        [.. registry.Panels
            .OrderBy(panel => panel.DefaultDock)
            .ThenBy(panel => panel.Order)
            .Select(panel => new PanelViewModel(panel, panel.CreateViewModel(services)))];
        StatusBar = new StatusBarViewModel(timeProvider, dispatcher);
        StatusBar.PropertyChanged += OnStatusBarChanged;
        Documents.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Title));
    }

    /// <inheritdoc/>
    public event EventHandler? CommandStatesChanged;

    /// <summary>Raised by <see cref="NotifyModelRenamed"/>: a class or member was renamed through a view model, and the model does not notify.</summary>
    public event EventHandler? ModelRenamed;

    /// <summary>Gets or sets the open project session, or null while the start page shows.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial ProjectSessionViewModel? Session { get; set; }

    /// <summary>Gets the open documents in tab order.</summary>
    public ObservableCollection<DocumentViewModel> Documents { get; } = [];

    /// <summary>Gets or sets the active document, or null.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial DocumentViewModel? ActiveDocument { get; set; }

    /// <summary>Gets the registered panels, by default dock and order.</summary>
    public IReadOnlyList<PanelViewModel> Panels => panels;

    internal IContributionRegistry Registry => registry;

    /// <summary>Gets the status bar.</summary>
    public StatusBarViewModel StatusBar { get; }

    /// <summary>Gets the status message, or null.</summary>
    public string? StatusMessage => StatusBar.Message;

    /// <summary>Gets or sets what is selected in the project tree: a class, a graph or a variable of the open project, or null.</summary>
    [ObservableProperty]
    public partial object? TreeSelection { get; set; }

    /// <summary>Gets or sets what hosts the documents and panels; set by the composition once the docking adapter exists.</summary>
    [ObservableProperty]
    public partial IShellLayoutHost? Layout { get; set; }

    /// <summary>Gets the menu bar, or null until <see cref="AttachCommands"/> ran.</summary>
    [ObservableProperty]
    public partial MenuBarViewModel? MenuBar { get; private set; }

    /// <summary>Gets the command bar, or null until <see cref="AttachCommands"/> ran.</summary>
    [ObservableProperty]
    public partial CommandBarViewModel? CommandBar { get; private set; }

    /// <summary>Gets the invoker the key bindings and menus run commands through, or null until <see cref="AttachCommands"/> ran.</summary>
    [ObservableProperty]
    public partial CommandInvoker? Commands { get; private set; }

    /// <summary>Gets the number of errors the open project's last compile found.</summary>
    public int CompileErrorCount => Session?.Project.LastDiagnostics.Count(diagnostic => diagnostic.Severity == CodeDiagnosticSeverity.Error) ?? 0;

    /// <summary>Generates the menu bar and the command bar from the registry, over an invoker built on this shell's context provider.</summary>
    /// <param name="invoker">Runs and queries the commands.</param>
    public void AttachCommands(CommandInvoker invoker)
    {
        ArgumentNullException.ThrowIfNull(invoker);
        Commands = invoker;
        MenuBar?.Dispose();
        CommandBar?.Dispose();
        MenuBar = new MenuBarViewModel(registry, invoker);
        CommandBar = new CommandBarViewModel(registry, invoker, () => CompileErrorCount);
    }

    /// <summary>Gives every panel that needs the shell its services; call once the layout and the invoker exist.</summary>
    /// <param name="api">The shell API.</param>
    /// <param name="invoker">Runs and queries the commands.</param>
    /// <param name="context">Host services shared across the editor.</param>
    public void AttachPanels(IShell api, CommandInvoker invoker, EditorContext context)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(invoker);
        ArgumentNullException.ThrowIfNull(context);
        var panelContext = new PanelContext(this, api, invoker, context);
        foreach (IShellPanelContent content in panels.Select(panel => panel.Content).OfType<IShellPanelContent>())
        {
            content.Attach(panelContext);
        }
    }

    /// <summary>Gets the window title (contracts/shell.md section 4).</summary>
    public string Title => TitleFormatter.Format(Session?.Project.Name, ActiveDocument?.Title, Session?.Unsaved.HasUnsavedFiles ?? false);

    /// <summary>Finds an open document.</summary>
    /// <param name="id">The document id.</param>
    /// <returns>The document, or null when it is not open.</returns>
    public DocumentViewModel? FindDocument(DocumentId id) => Documents.FirstOrDefault(document => document.Id == id);

    /// <summary>Finds a panel.</summary>
    /// <param name="panelId">The panel id.</param>
    /// <returns>The panel, or null when none is registered with that id.</returns>
    public PanelViewModel? FindPanel(string panelId) => panels.Find(panel => string.Equals(panel.Id, panelId, StringComparison.Ordinal));

    /// <summary>Adds a document to the open ones unless one with its id is open already.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The open document with that id: <paramref name="document"/>, or the one that was open already.</returns>
    public DocumentViewModel AddDocument(DocumentViewModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (FindDocument(document.Id) is { } open)
        {
            return open;
        }

        Documents.Add(document);
        return document;
    }

    /// <summary>Removes and disposes an open document; when it was active, the previous one (or the first) becomes active.</summary>
    /// <param name="id">The document id.</param>
    /// <returns>Whether a document was removed.</returns>
    public bool RemoveDocument(DocumentId id)
    {
        if (FindDocument(id) is not { } document)
        {
            return false;
        }

        int index = Documents.IndexOf(document);
        Documents.RemoveAt(index);
        if (ReferenceEquals(ActiveDocument, document))
        {
            ActiveDocument = Documents.Count == 0 ? null : Documents[Math.Max(index - 1, 0)];
        }

        document.Dispose();
        return true;
    }

    /// <summary>Tells the panels that show names (the project tree) that a class or member was renamed; the model of a class or an event graph does not notify.</summary>
    public void NotifyModelRenamed() => ModelRenamed?.Invoke(this, EventArgs.Empty);

    /// <summary>Shows a status message.</summary>
    /// <param name="message">The text.</param>
    /// <param name="expiry">How long it stays, or null to keep it until replaced.</param>
    public void ShowStatus(string message, TimeSpan? expiry = null) => StatusBar.Show(message, expiry);

    /// <summary>Stops following the session and the active document, and disposes the open documents.</summary>
    public void Dispose()
    {
        Unfollow();
        foreach (IShellPanelContent content in panels.Select(panel => panel.Content).OfType<IShellPanelContent>())
        {
            content.Detach();
        }

        MenuBar?.Dispose();
        CommandBar?.Dispose();
        StatusBar.PropertyChanged -= OnStatusBarChanged;
        StatusBar.Dispose();
        foreach (DocumentViewModel document in Documents)
        {
            document.Dispose();
        }

        Documents.Clear();
    }

    partial void OnSessionChanged(ProjectSessionViewModel? oldValue, ProjectSessionViewModel? newValue)
    {
        if (followedSession is not null)
        {
            followedSession.CommandStatesChanged -= OnSessionPulse;
            followedSession.Saved -= OnSessionSaved;
            followedSession.StatusReported -= OnSessionStatus;
        }

        if (followedProject is not null)
        {
            followedProject.PropertyChanged -= OnProjectChanged;
        }

        followedSession = newValue;
        followedProject = newValue?.Project;
        if (newValue is not null)
        {
            newValue.CommandStatesChanged += OnSessionPulse;
            newValue.Saved += OnSessionSaved;
            newValue.StatusReported += OnSessionStatus;
            newValue.Project.PropertyChanged += OnProjectChanged;
        }

        TreeSelection = null;
        UpdateBuildState();
        OnPropertyChanged(nameof(CompileErrorCount));
        RaiseCommandStatesChanged();
    }

    partial void OnTreeSelectionChanged(object? oldValue, object? newValue) => RaiseCommandStatesChanged();

    partial void OnActiveDocumentChanged(DocumentViewModel? oldValue, DocumentViewModel? newValue)
    {
        if (followedDocument is not null)
        {
            followedDocument.PropertyChanged -= OnDocumentChanged;
        }

        followedDocument = newValue;
        if (newValue is not null)
        {
            newValue.PropertyChanged += OnDocumentChanged;
        }

        RaiseCommandStatesChanged();
    }

    private void Unfollow()
    {
        if (followedProject is not null)
        {
            followedProject.PropertyChanged -= OnProjectChanged;
            followedProject = null;
        }

        if (followedSession is not null)
        {
            followedSession.CommandStatesChanged -= OnSessionPulse;
            followedSession.Saved -= OnSessionSaved;
            followedSession.StatusReported -= OnSessionStatus;
            followedSession = null;
        }

        if (followedDocument is not null)
        {
            followedDocument.PropertyChanged -= OnDocumentChanged;
            followedDocument = null;
        }
    }

    private void OnSessionSaved(object? sender, int files) => ShowStatus($"Saved {files} file(s)", SessionStatus.TransientLifetime);

    private void OnSessionStatus(object? sender, SessionStatus status) => ShowStatus(status.Text, status.Expiry);

    private void OnSessionPulse(object? sender, EventArgs e)
    {
        UpdateBuildState();
        OnPropertyChanged(nameof(Title));
        RaiseCommandStatesChanged();
    }

    private void OnProjectChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Project.LastDiagnostics))
        {
            OnPropertyChanged(nameof(CompileErrorCount));
            CommandBar?.RefreshBadge();
        }
    }

    private void OnDocumentChanged(object? sender, PropertyChangedEventArgs e) => OnPropertyChanged(nameof(Title));

    private void OnStatusBarChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StatusBarViewModel.Message))
        {
            OnPropertyChanged(nameof(StatusMessage));
        }
    }

    private void UpdateBuildState() => StatusBar.SetBuildState(Session switch
    {
        { IsBuilding: true } => BuildState.Building,
        { IsRunning: true } => BuildState.Running,
        _ => BuildState.Idle,
    });

    /// <summary>Tells the menus and bars that the layout changed (a pane or tab floated, docked or reset), so the commands that depend on it are enabled again.</summary>
    internal void NotifyLayoutChanged() => RaiseCommandStatesChanged();

    private void RaiseCommandStatesChanged() => CommandStatesChanged?.Invoke(this, EventArgs.Empty);
}
