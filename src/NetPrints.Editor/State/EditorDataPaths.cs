using System.Security.Cryptography;
using System.Text;

namespace NetPrints.Editor.State;

/// <summary>Where the editor keeps its per-user files (state-files.md §1).</summary>
public sealed class EditorDataPaths
{
    /// <summary>The environment variable that replaces <c>&lt;ApplicationData&gt;/NetPrints</c>, for tests and E2E workers.</summary>
    public const string StateDirectoryVariable = "NETPRINTS_STATE_DIR";

    private const string RootFolderName = "NetPrints";
    private const int ProjectKeyLength = 16;

    /// <summary>Creates the paths under <paramref name="root"/>.</summary>
    /// <param name="root">The folder that holds every per-user file of the editor.</param>
    public EditorDataPaths(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);
        Root = root;
    }

    /// <summary>Gets the folder that holds every per-user file of the editor.</summary>
    public string Root { get; }

    /// <summary>Gets the folder of the window, layout, recent and session files.</summary>
    public string StateDirectory => Path.Combine(Root, "state");

    /// <summary>Gets the folder of the per-project session files.</summary>
    public string SessionsDirectory => Path.Combine(StateDirectory, "sessions");

    /// <summary>Gets the folder of the per-project backup folders.</summary>
    public string BackupsDirectory => Path.Combine(Root, "backups");

    /// <summary>Resolves the paths of this process: <c>NETPRINTS_STATE_DIR</c> when set, else <c>&lt;ApplicationData&gt;/NetPrints</c>.</summary>
    /// <returns>The paths.</returns>
    public static EditorDataPaths Resolve() =>
        Resolve(Environment.GetEnvironmentVariable, Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    /// <summary>Resolves the paths from an environment and an application data folder.</summary>
    /// <param name="getEnvironmentVariable">Reads an environment variable, or returns null when it is not set.</param>
    /// <param name="applicationData">The user's application data folder.</param>
    /// <returns>The paths.</returns>
    public static EditorDataPaths Resolve(Func<string, string?> getEnvironmentVariable, string applicationData)
    {
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        string? overridden = getEnvironmentVariable(StateDirectoryVariable);
        return new EditorDataPaths(string.IsNullOrEmpty(overridden) ? Path.Combine(applicationData, RootFolderName) : overridden);
    }

    /// <summary>Gets the backup folder of a project.</summary>
    /// <param name="projectKey">The project's <see cref="ProjectKey(string)"/>.</param>
    /// <returns>The folder path.</returns>
    public string BackupDirectoryOf(string projectKey) => Path.Combine(BackupsDirectory, projectKey);

    /// <summary>Gets the key that names a project's per-user files: the first 16 lowercase hex characters of the SHA-256 of its full path.</summary>
    /// <param name="projectFilePath">The project file's path.</param>
    /// <returns>The key; the path is case-folded first on Windows.</returns>
    public static string ProjectKey(string projectFilePath) => ProjectKey(projectFilePath, OperatingSystem.IsWindows());

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
