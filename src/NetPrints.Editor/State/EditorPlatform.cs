namespace NetPrints.Editor.State;

/// <summary>An operating system family and where it keeps the editor's state and log folders (ADR-0033).</summary>
public abstract class EditorPlatform
{
    private const string AppFolder = "NetPrints";
    private const string LogsFolder = "logs";
    private const string XdgStateHomeVariable = "XDG_STATE_HOME";

    private EditorPlatform()
    {
    }

    /// <summary>Gets Windows.</summary>
    public static EditorPlatform Windows { get; } = new WindowsPlatform();

    /// <summary>Gets macOS.</summary>
    public static EditorPlatform MacOS { get; } = new MacOSPlatform();

    /// <summary>Gets Linux and the other Unix systems.</summary>
    public static EditorPlatform Linux { get; } = new LinuxPlatform();

    /// <summary>Gets the platform this process runs on.</summary>
    public static EditorPlatform Current =>
        OperatingSystem.IsWindows() ? Windows : OperatingSystem.IsMacOS() ? MacOS : Linux;

    /// <summary>Gets the folder that holds <c>state/</c> and <c>backups/</c> when no variable replaces it.</summary>
    /// <param name="getEnvironmentVariable">Reads an environment variable, or returns null when it is not set.</param>
    /// <param name="folders">The user's folders.</param>
    /// <returns>The state root.</returns>
    public abstract string StateRoot(Func<string, string?> getEnvironmentVariable, UserFolders folders);

    /// <summary>Gets the folder of the session logs when no variable replaces it.</summary>
    /// <param name="getEnvironmentVariable">Reads an environment variable, or returns null when it is not set.</param>
    /// <param name="folders">The user's folders.</param>
    /// <returns>The log folder.</returns>
    public virtual string LogDirectory(Func<string, string?> getEnvironmentVariable, UserFolders folders) =>
        Path.Combine(StateRoot(getEnvironmentVariable, folders), LogsFolder);

    private sealed class WindowsPlatform : EditorPlatform
    {
        public override string StateRoot(Func<string, string?> getEnvironmentVariable, UserFolders folders) =>
            Path.Combine(folders.LocalApplicationData, AppFolder);
    }

    private sealed class MacOSPlatform : EditorPlatform
    {
        public override string StateRoot(Func<string, string?> getEnvironmentVariable, UserFolders folders) =>
            Path.Combine(folders.Home, "Library", "Application Support", AppFolder);

        public override string LogDirectory(Func<string, string?> getEnvironmentVariable, UserFolders folders) =>
            Path.Combine(folders.Home, "Library", "Logs", AppFolder);
    }

    private sealed class LinuxPlatform : EditorPlatform
    {
        public override string StateRoot(Func<string, string?> getEnvironmentVariable, UserFolders folders)
        {
            ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
            string? xdg = getEnvironmentVariable(XdgStateHomeVariable);
            string stateHome = !string.IsNullOrEmpty(xdg) && Path.IsPathRooted(xdg) ? xdg : Path.Combine(folders.Home, ".local", "state");
            return Path.Combine(stateHome, AppFolder);
        }
    }
}
