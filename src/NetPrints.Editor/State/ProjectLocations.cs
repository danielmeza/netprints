namespace NetPrints.Editor.State;

/// <summary>The parent folder new projects and copied samples go in: the one the user used last, or the default under the documents folder.</summary>
public sealed class ProjectLocations
{
    /// <summary>The name of the folder under the documents folder that holds new projects by default.</summary>
    public const string DefaultFolderName = "NetPrints";

    private const string OverrideFolderName = "Documents";

    private readonly IEditorStateStore? store;

    /// <summary>Creates the locations over a state store.</summary>
    /// <param name="store">Where the last location is kept, or <see langword="null"/> to keep none.</param>
    /// <param name="documentsFolder">The user's documents folder; the default location is its <c>NetPrints</c> folder.</param>
    public ProjectLocations(IEditorStateStore? store, string documentsFolder)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentsFolder);
        this.store = store;
        DefaultLocation = Path.Combine(documentsFolder, DefaultFolderName);
    }

    /// <summary>Gets the default location, the <c>NetPrints</c> folder under the documents folder.</summary>
    public string DefaultLocation { get; }

    /// <summary>Gets the location to offer: the last one remembered, or <see cref="DefaultLocation"/>.</summary>
    public string Last => store?.LoadStart()?.NewProjectLocation is { Length: > 0 } remembered ? remembered : DefaultLocation;

    /// <summary>Resolves the locations of the running editor.</summary>
    /// <param name="store">Where the last location is kept, or <see langword="null"/> for none.</param>
    /// <param name="paths">The editor's data folders.</param>
    /// <param name="getEnvironmentVariable">Reads an environment variable.</param>
    /// <param name="documentsFolder">The OS documents folder, or an empty string when it has none.</param>
    /// <param name="homeFolder">The user's home folder, used when <paramref name="documentsFolder"/> is empty.</param>
    /// <returns>The locations; with <c>NETPRINTS_STATE_DIR</c> set, the default is a <c>Documents</c> folder inside it, so tests and E2E runs never use the real documents folder.</returns>
    public static ProjectLocations Resolve(IEditorStateStore? store, EditorDataPaths paths, Func<string, string?> getEnvironmentVariable, string documentsFolder, string homeFolder)
    {
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(getEnvironmentVariable);
        if (!string.IsNullOrEmpty(getEnvironmentVariable(EditorDataPaths.StateDirectoryVariable)))
        {
            return new ProjectLocations(store, Path.Combine(paths.Root, OverrideFolderName));
        }

        return new ProjectLocations(store, string.IsNullOrEmpty(documentsFolder) ? homeFolder : documentsFolder);
    }

    /// <summary>Creates the locations for the current user, in their documents folder.</summary>
    /// <param name="store">Where the last location is kept, or <see langword="null"/> for none.</param>
    /// <returns>The locations.</returns>
    public static ProjectLocations ForCurrentUser(IEditorStateStore? store)
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return new ProjectLocations(store, string.IsNullOrEmpty(documents) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : documents);
    }

    /// <summary>Remembers the location the user used, so the next New project and sample start there.</summary>
    /// <param name="location">The parent folder.</param>
    public void Remember(string location)
    {
        ArgumentException.ThrowIfNullOrEmpty(location);
        store?.Update(state => state with { NewProjectLocation = location });
    }
}
