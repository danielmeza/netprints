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
    private readonly HashSet<string> newerFiles = new(StringComparer.Ordinal);
    private readonly HashSet<string> skipped = new(StringComparer.Ordinal);

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
    public void SaveWindow(WindowState state) => Save(WindowPath, state, StateJsonContext.Default.WindowState, userChanged: false);

    /// <inheritdoc/>
    public LayoutState? LoadLayout() => Load(LayoutPath, StateJsonContext.Default.LayoutState);

    /// <inheritdoc/>
    public void SaveLayout(LayoutState state, bool userChanged = false) => Save(LayoutPath, state, StateJsonContext.Default.LayoutState, userChanged);

    /// <inheritdoc/>
    public RecentState LoadRecent() =>
        Load(RecentPath, StateJsonContext.Default.RecentState) is { Entries: not null } state && Valid(RecentPath, !state.Entries.Any(entry => entry is null))
            ? state
            : RecentState.Empty;

    /// <inheritdoc/>
    public void SaveRecent(RecentState state) => Save(RecentPath, state, StateJsonContext.Default.RecentState, userChanged: false);

    /// <inheritdoc/>
    public StartState? LoadStart() => Load(StartPath, StateJsonContext.Default.StartState);

    /// <inheritdoc/>
    public void SaveStart(StartState state, bool userChanged = false) => Save(StartPath, state, StateJsonContext.Default.StartState, userChanged);

    /// <inheritdoc/>
    public SessionState? LoadSession(string projectPath) =>
        Load(SessionPath(projectPath), StateJsonContext.Default.SessionState) is { OpenDocuments: not null, Viewports: not null } state
            && Valid(SessionPath(projectPath), !state.OpenDocuments.Any(document => document is null) && !state.Viewports.Values.Any(viewport => viewport is null))
                ? state
                : null;

    /// <inheritdoc/>
    public void SaveSession(SessionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        Save(SessionPath(state.ProjectPath), state, StateJsonContext.Default.SessionState, userChanged: false);
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
            byte[] bytes = fileSystem.ReadAllBytes(path);
            if (IsNewer(bytes))
            {
                newerFiles.Add(path);
                Log.StateFileUnsupported(logger, path);
                return null;
            }

            T? state = JsonSerializer.Deserialize(bytes, typeInfo);
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

    private bool Valid(string path, bool valid)
    {
        if (!valid)
        {
            Log.StateFileUnsupported(logger, path);
        }

        return valid;
    }

    private static bool IsNewer(byte[] bytes)
    {
        using JsonDocument document = JsonDocument.Parse(bytes);
        return document.RootElement.ValueKind == JsonValueKind.Object
            && document.RootElement.TryGetProperty("schemaVersion", out JsonElement version)
            && version.ValueKind == JsonValueKind.Number
            && version.TryGetInt32(out int number)
            && number > StateFile.CurrentVersion;
    }

    private void Save<T>(string path, T state, JsonTypeInfo<T> typeInfo, bool userChanged)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (newerFiles.Contains(path) && !userChanged)
        {
            if (skipped.Add(path))
            {
                Log.StateFileSaveSkipped(logger, path);
            }

            return;
        }

        string text = JsonSerializer.Serialize(state, typeInfo).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        try
        {
            writer.Write(path, new UTF8Encoding(false).GetBytes(text));
            newerFiles.Remove(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.StateFileWriteFailed(logger, ex, path);
        }
    }
}
