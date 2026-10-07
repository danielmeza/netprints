using Avalonia.Controls;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// Keeps the shell's per-user state: it restores the dock layout when the shell is composed and saves it after every change,
/// saves the open project's session when the project is replaced or the window closes, and restores the session of the project
/// that opens. The persistence handler is subscribed before the window guard, so it saves before the guard cancels a close.
/// </summary>
internal sealed class ShellStatePersistence : IDisposable
{
    private static readonly TimeSpan LayoutDelay = TimeSpan.FromSeconds(1);

    private readonly ShellViewModel shell;
    private readonly DockShellAdapter adapter;
    private readonly Window window;
    private readonly SessionService sessions;
    private readonly LayoutSaver layoutSaver;
    private string? projectPath;

    /// <summary>Restores the saved layout and starts following the layout and the window.</summary>
    /// <param name="shell">The shell state.</param>
    /// <param name="adapter">The docking adapter whose layout is kept.</param>
    /// <param name="window">The shell window.</param>
    /// <param name="store">Where the layout and the sessions are kept.</param>
    /// <param name="time">The clock of the layout save delay.</param>
    /// <param name="dispatcher">Brings the delayed save to the UI thread.</param>
    public ShellStatePersistence(ShellViewModel shell, DockShellAdapter adapter, Window window, IEditorStateStore store, TimeProvider time, IUiDispatcher dispatcher)
    {
        this.shell = shell;
        this.adapter = adapter;
        this.window = window;
        sessions = new SessionService(store);
        adapter.RestoreLayout(store.LoadLayout());
        layoutSaver = new LayoutSaver(adapter, store, time, dispatcher, LayoutDelay);
        window.Closing += OnClosing;
    }

    /// <summary>Saves the session of the project that is open now.</summary>
    public void SaveSession()
    {
        if (projectPath is { } path)
        {
            sessions.Save(path, adapter, shell.FindDocument);
        }
    }

    /// <summary>Makes <paramref name="session"/> the project the state belongs to and reopens what it had open.</summary>
    /// <param name="session">The session that was just opened, or null when the start page shows.</param>
    public void RestoreSession(ProjectSessionViewModel? session)
    {
        projectPath = session?.ProjectFilePath;
        if (projectPath is { } path)
        {
            sessions.Restore(path, adapter, shell.FindDocument);
        }
    }

    /// <summary>Stops following the window and the layout.</summary>
    public void Dispose()
    {
        window.Closing -= OnClosing;
        layoutSaver.Dispose();
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (e.Cancel)
        {
            return;
        }

        SaveSession();
        layoutSaver.SaveNow();
    }
}
