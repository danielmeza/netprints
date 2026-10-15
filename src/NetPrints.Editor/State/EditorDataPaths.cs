using System.Security.Cryptography;
using System.Text;

namespace NetPrints.Editor.State;

/// <summary>Where the editor keeps its per-user files (state-files.md §1).</summary>
public sealed class EditorDataPaths
{
    /// <summary>The environment variable that replaces the per-OS state root (not <c>settings.json</c>), for tests and E2E workers.</summary>
    public const string StateDirectoryVariable = "NETPRINTS_STATE_DIR";

    private const string RootFolderName = "NetPrints";
    private const int ProjectKeyLength = 16;

    /// <summary>The environment variable that replaces the log folder.</summary>
    public const string LogDirectoryVariable = "NETPRINTS_LOG_DIR";

    /// <summary>Creates the paths under <paramref name="root"/>.</summary>
    /// <param name="root">The folder that holds the state and backup folders.</param>
    /// <param name="logDirectory">The folder of the session logs; <c>&lt;root&gt;/logs</c> when null.</param>
    /// <param name="configDirectory">The folder of the user-editable settings; <paramref name="root"/> when null.</param>
    /// <param name="legacyRoot">The former root whose <c>state/</c> and <c>backups/</c> are migrated, or null when there is none.</param>
    public EditorDataPaths(string root, string? logDirectory = null, string? configDirectory = null, string? legacyRoot = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        Root = root;
        LogDirectory = logDirectory ?? Path.Combine(root, "logs");
        ConfigDirectory = configDirectory ?? root;
        LegacyRoot = legacyRoot;
    }

    /// <summary>Gets the folder that holds the state and backup folders.</summary>
    public string Root { get; }

    /// <summary>Gets the folder of the session logs.</summary>
    public string LogDirectory { get; }

    /// <summary>Gets the folder of the user-editable settings (the config folder, never moved).</summary>
    public string ConfigDirectory { get; }

    /// <summary>Gets the former root that held <c>state/</c> and <c>backups/</c> before ADR-0033, or null when nothing is to be migrated.</summary>
    public string? LegacyRoot { get; }

    /// <summary>Gets the folder of the window, layout, recent and session files.</summary>
    public string StateDirectory => Path.Combine(Root, "state");

    /// <summary>Gets the folder of the per-project session files.</summary>
    public string SessionsDirectory => Path.Combine(StateDirectory, "sessions");

    /// <summary>Gets the folder of the per-project backup folders.</summary>
    public string BackupsDirectory => Path.Combine(Root, "backups");

    /// <summary>Resolves the paths of this process on this platform (ADR-0033).</summary>
    /// <returns>The paths.</returns>
    public static EditorDataPaths Resolve() =>
        Resolve(Environment.GetEnvironmentVariable, EditorPlatform.Current, UserFolders.Current);

    /// <summary>Resolves the paths from an environment, a platform and the user's folders, without touching the disk.</summary>
    /// <param name="getEnvironmentVariable">Reads an environment variable, or returns null when it is not set.</param>
    /// <param name="platform">The operating system family.</param>
    /// <param name="folders">The user's folders.</param>
    /// <returns>The paths: <c>NETPRINTS_STATE_DIR</c> replaces the root; <c>NETPRINTS_LOG_DIR</c> beats <c>&lt;root&gt;/logs</c> of a replaced root, which beats the platform default.</returns>
    public static EditorDataPaths Resolve(Func<string, string?> getEnvironmentVariable, EditorPlatform platform, UserFolders folders)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(folders);
        string configDirectory = Path.Combine(folders.ApplicationData, RootFolderName);
        string? stateOverride = getEnvironmentVariable(StateDirectoryVariable);
        string? logOverride = getEnvironmentVariable(LogDirectoryVariable);
        bool stateReplaced = !string.IsNullOrEmpty(stateOverride);
        string root = stateOverride is { Length: > 0 } ? stateOverride : platform.StateRoot(getEnvironmentVariable, folders);
        string logDirectory = !string.IsNullOrEmpty(logOverride)
            ? logOverride
            : stateReplaced ? Path.Combine(root, "logs") : platform.LogDirectory(getEnvironmentVariable, folders);
        return new EditorDataPaths(root, logDirectory, configDirectory, stateReplaced ? null : configDirectory);
    }

    /// <summary>Gets a value indicating whether paths are compared ignoring case: on Windows and on macOS, whose volumes are case-insensitive by default.</summary>
    public static bool PathsAreCaseInsensitive => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

    /// <summary>Gets how paths are compared on this platform.</summary>
    public static StringComparison PathComparison => PathsAreCaseInsensitive ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    /// <summary>Gets the backup folder of a project.</summary>
    /// <param name="projectKey">The project's <see cref="ProjectKey(string)"/>.</param>
    /// <returns>The folder path.</returns>
    public string BackupDirectoryOf(string projectKey) => Path.Combine(BackupsDirectory, projectKey);

    /// <summary>Gets the key that names a project's per-user files: the first 16 lowercase hex characters of the SHA-256 of its full path.</summary>
    /// <param name="projectFilePath">The project file's path.</param>
    /// <returns>The key; the path is case-folded first on Windows and macOS.</returns>
    public static string ProjectKey(string projectFilePath) => ProjectKey(projectFilePath, PathsAreCaseInsensitive);

    /// <summary>Gets a project key, folding the case of the path or not.</summary>
    /// <param name="projectFilePath">The project file's path.</param>
    /// <param name="caseFold">Whether to case-fold the full path before hashing.</param>
    /// <returns>The key.</returns>
    public static string ProjectKey(string projectFilePath, bool caseFold)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFilePath);
        string fullPath = Path.GetFullPath(projectFilePath);
        if (caseFold)
        {
            fullPath = fullPath.ToUpperInvariant();
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(fullPath)))[..ProjectKeyLength];
    }
}
