using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Contributions;

/// <summary>What a panel's view model factory is given: the shell, the dialogs, the Output panel and the extension's services.</summary>
/// <param name="Shell">The shell.</param>
/// <param name="Dialogs">The dialogs.</param>
/// <param name="Output">The Output panel.</param>
/// <param name="Services">The extension's own services.</param>
internal sealed record PanelFactoryContext(IEditorShell Shell, IDialogService Dialogs, IEditorOutput Output, IServiceProvider Services) : IPanelContext
{
    /// <summary>Gets a context for hosts that show no panel content: nothing is shown, nothing is written.</summary>
    public static PanelFactoryContext Detached { get; } = new(new DetachedShell(), new DetachedDialogs(), new DetachedOutput(), EmptyServiceProvider.Instance);

    private sealed class DetachedShell : IEditorShell
    {
        public bool IsPanelVisible(string panelId) => false;

        public void ShowPanel(string panelId)
        {
        }

        public void HidePanel(string panelId)
        {
        }

        public void ResetLayout()
        {
        }
    }

    private sealed class DetachedDialogs : IDialogService
    {
        public void ShowNotification(string title, string message)
        {
        }

        public Task ShowErrorAsync(string title, string message) => Task.CompletedTask;

        public Task<object> ShowDialogAsync(string dialogId, CancellationToken cancellationToken = default) =>
            Task.FromException<object>(new InvalidOperationException("The shell is not composed yet."));
    }

    private sealed class DetachedOutput : IEditorOutput
    {
        public void WriteLine(string text)
        {
        }
    }
}

/// <summary>
/// What a start page tile's view model factory is given: the panel-level services plus the flows and launchers the built-in tiles use.
/// </summary>
internal sealed class StartPageContext : IStartPageContext
{
    private readonly IPanelContext host;

    /// <summary>Initializes a new instance of the <see cref="StartPageContext"/> class.</summary>
    /// <param name="projectActions">The project flows.</param>
    /// <param name="host">The shell, dialogs, output and services.</param>
    /// <param name="recent">The recent projects, or <see langword="null"/> when the editor keeps none.</param>
    /// <param name="stateStore">The editor state, or <see langword="null"/> when the editor keeps none.</param>
    /// <param name="clipboard">The clipboard, or <see langword="null"/> when there is none.</param>
    /// <param name="folderLauncher">Opens a folder in the file manager, or <see langword="null"/> when there is none.</param>
    /// <param name="urlLauncher">Opens a link; the system browser when not given.</param>
    /// <param name="samples">The bundled samples; the editor's own when not given.</param>
    /// <param name="time">The clock; the system clock when not given.</param>
    public StartPageContext(
        IProjectActions projectActions,
        IPanelContext host,
        RecentProjects? recent = null,
        IEditorStateStore? stateStore = null,
        IClipboardService? clipboard = null,
        IFolderLauncher? folderLauncher = null,
        IUrlLauncher? urlLauncher = null,
        SampleCatalog? samples = null,
        TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(projectActions);
        ArgumentNullException.ThrowIfNull(host);
        this.host = host;
        ProjectActions = projectActions;
        Recent = recent;
        StateStore = stateStore;
        Clipboard = clipboard;
        FolderLauncher = folderLauncher;
        UrlLauncher = urlLauncher ?? new ShellUrlLauncher();
        Samples = samples ?? SampleCatalog.Bundled;
        Time = time ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public IEditorShell Shell => host.Shell;

    /// <inheritdoc/>
    public IDialogService Dialogs => host.Dialogs;

    /// <inheritdoc/>
    public IEditorOutput Output => host.Output;

    /// <inheritdoc/>
    public IServiceProvider Services => host.Services;

    /// <summary>Gets the project flows.</summary>
    public IProjectActions ProjectActions { get; }

    /// <summary>Gets the recent projects, or <see langword="null"/>.</summary>
    public RecentProjects? Recent { get; }

    /// <summary>Gets the editor state, or <see langword="null"/>.</summary>
    public IEditorStateStore? StateStore { get; }

    /// <summary>Gets the clipboard, or <see langword="null"/>.</summary>
    public IClipboardService? Clipboard { get; }

    /// <summary>Gets the launcher of folders, or <see langword="null"/>.</summary>
    public IFolderLauncher? FolderLauncher { get; }

    /// <summary>Gets the launcher of links.</summary>
    public IUrlLauncher UrlLauncher { get; }

    /// <summary>Gets the bundled samples.</summary>
    public SampleCatalog Samples { get; }

    /// <summary>Gets the clock.</summary>
    public TimeProvider Time { get; }

    /// <summary>Gets the editor's own context behind the typed one.</summary>
    /// <param name="context">The context a tile factory was given.</param>
    /// <returns>The context the editor built.</returns>
    /// <exception cref="InvalidOperationException">The context was built by something other than the editor's shell.</exception>
    public static StartPageContext From(IStartPageContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context as StartPageContext ?? throw new InvalidOperationException("The editor's own tiles run only in the editor's start page.");
    }
}

/// <summary>The status bar, settings page and dialog contexts of the editor host, over a command context.</summary>
internal sealed class EditorContexts(ICommandContext command) : IStatusBarContext, ISettingsPageContext, IDialogContext
{
    /// <inheritdoc/>
    public IEditorShell Shell => command.Shell;

    /// <inheritdoc/>
    public IProjectSession? Session => command.Session;

    /// <inheritdoc/>
    public IGraphEditor? ActiveGraph => command.ActiveGraph;

    /// <inheritdoc/>
    public IDialogService Dialogs => command.Dialogs;

    /// <inheritdoc/>
    public IServiceProvider Services => command.Services;
}
