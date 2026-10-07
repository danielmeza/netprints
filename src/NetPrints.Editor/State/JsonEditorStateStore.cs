using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.Extensions.Logging;

namespace NetPrints.Editor.State;

/// <summary>
/// <see cref="IEditorStateStore"/> over JSON files under <see cref="EditorDataPaths.StateDirectory"/> (state-files.md §2): UTF-8
/// without a byte order mark, LF line endings, every write through <see cref="AtomicFileWriter"/>. Not thread safe.
/// </summary>
public sealed class JsonEditorStateStore : IEditorStateStore
{
    private const string WindowFileName = "window.json";
    private const string LayoutFileName = "layout.json";
    private const string RecentFileName = "recent.json";
    private const string StartFileName = "start.json";

    private readonly EditorDataPaths paths;
    private readonly IEditorFileSystem fileSystem;
    private readonly AtomicFileWriter writer;
    private readonly ILogger logger;

    /// <summary>Creates the store.</summary>
    /// <param name="paths">The editor's data folders.</param>
    /// <param name="fileSystem">The file system to use.</param>
    /// <param name="logger">Receives the warnings about files that cannot be used.</param>
    public JsonEditorStateStore(EditorDataPaths paths, IEditorFileSystem fileSystem, ILogger logger)
    {
        this.paths = paths ?? throw new ArgumentNullException(nameof(paths));
        this.fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        writer = new AtomicFileWriter(fileSystem);
    }

    /// <inheritdoc/>
    public WindowState? LoadWindow() => Load(WindowPath, StateJsonContext.Default.WindowState);

    /// <inheritdoc/>
    public void SaveWindow(WindowState state) => Save(WindowPath, state, StateJsonContext.Default.WindowState);

    /// <inheritdoc/>
    public LayoutState? LoadLayout() => Load(LayoutPath, StateJsonContext.Default.LayoutState);

    /// <inheritdoc/>
    public void SaveLayout(LayoutState state) => Save(LayoutPath, state, StateJsonContext.Default.LayoutState);

    /// <inheritdoc/>
    public RecentState LoadRecent() => Load(RecentPath, StateJsonContext.Default.RecentState) is { Entries: not null } state ? state : RecentState.Empty;

    /// <inheritdoc/>
    public void SaveRecent(RecentState state) => Save(RecentPath, state, StateJsonContext.Default.RecentState);

    /// <inheritdoc/>
    public StartState? LoadStart() => Load(StartPath, StateJsonContext.Default.StartState);

    /// <inheritdoc/>
    public void SaveStart(StartState state) => Save(StartPath, state, StateJsonContext.Default.StartState);

    /// <inheritdoc/>
    public SessionState? LoadSession(string projectPath) =>
        Load(SessionPath(projectPath), StateJsonContext.Default.SessionState) is { OpenDocuments: not null, Viewports: not null } state ? state : null;

    /// <inheritdoc/>
    public void SaveSession(SessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Save(SessionPath(state.ProjectPath), state, StateJsonContext.Default.SessionState);
    }

    private string WindowPath => Path.Combine(paths.StateDirectory, WindowFileName);

    private string LayoutPath => Path.Combine(paths.StateDirectory, LayoutFileName);

    private string RecentPath => Path.Combine(paths.StateDirectory, RecentFileName);

    private string StartPath => Path.Combine(paths.StateDirectory, StartFileName);

    private string SessionPath(string projectPath) => Path.Combine(paths.SessionsDirectory, EditorDataPaths.ProjectKey(projectPath) + ".json");

    private T? Load<T>(string path, JsonTypeInfo<T> typeInfo)
        where T : class, IStateFile
    {
        if (!fileSystem.FileExists(path))
        {
            return null;
        }

        try
        {
            T? state = JsonSerializer.Deserialize(fileSystem.ReadAllBytes(path), typeInfo);
            if (state is { SchemaVersion: StateFile.CurrentVersion })
            {
                return state;
            }

            Log.StateFileUnsupported(logger, path);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            Log.StateFileUnreadable(logger, ex, path);
        }

        return null;
    }

    private void Save<T>(string path, T state, JsonTypeInfo<T> typeInfo)
    {
        ArgumentNullException.ThrowIfNull(state);
        string text = JsonSerializer.Serialize(state, typeInfo).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        try
        {
            writer.Write(path, new UTF8Encoding(false).GetBytes(text));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.StateFileWriteFailed(logger, ex, path);
        }
    }
}
