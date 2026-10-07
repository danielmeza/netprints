using NetPrints.Editor.State;

namespace NetPrints.Editor.UITests.Hosting;

/// <summary>The per-user state files in memory.</summary>
internal sealed class MemoryStateStore : IEditorStateStore
{
    private readonly Dictionary<string, SessionState> sessions = [];

    public LayoutState? Layout { get; set; }

    public int LayoutSaves { get; private set; }

    public IReadOnlyDictionary<string, SessionState> Sessions => sessions;

    public WindowState? LoadWindow() => null;

    public void SaveWindow(WindowState state)
    {
    }

    public LayoutState? LoadLayout() => Layout;

    public void SaveLayout(LayoutState state)
    {
        LayoutSaves++;
        Layout = state;
    }

    public RecentState LoadRecent() => RecentState.Empty;

    public void SaveRecent(RecentState state)
    {
    }

    public SessionState? LoadSession(string projectPath) => sessions.GetValueOrDefault(projectPath);

    public void SaveSession(SessionState state) => sessions[state.ProjectPath] = state;
}
