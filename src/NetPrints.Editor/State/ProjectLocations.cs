namespace NetPrints.Editor.State;

/// <summary>The parent folder new projects and copied samples go in: the one the user used last, or the default under the documents folder.</summary>
public sealed class ProjectLocations
{
    /// <summary>The name of the folder under the documents folder that holds new projects by default.</summary>
    public const string DefaultFolderName = "NetPrints";

    private const int HomePrefixLength = 2;

    private readonly IEditorStateStore? store;
    private readonly string homeFolder;

    /// <summary>Creates the locations over a state store.</summary>
    /// <param name="store">Where the last location is kept, or <see langword="null"/> to keep none.</param>
    /// <param name="documentsFolder">The user's documents folder; the default location is its <c>NetPrints</c> folder.</param>
    /// <param name="homeFolder">The user's home folder, which a leading <c>~</c> in a location stands for; null for the current user's.</param>
    public ProjectLocations(IEditorStateStore? store, string documentsFolder, string? homeFolder = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentsFolder);
        this.store = store;
        this.homeFolder = string.IsNullOrEmpty(homeFolder) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : homeFolder;
        DefaultLocation = Path.Combine(documentsFolder, DefaultFolderName);
    }

    /// <summary>Gets the default location, the <c>NetPrints</c> folder under the documents folder.</summary>
    public string DefaultLocation { get; }

    /// <summary>Gets the location to offer: the last one remembered, or <see cref="DefaultLocation"/>.</summary>
    public string Last => store?.LoadStart()?.NewProjectLocation is { Length: > 0 } remembered ? remembered : DefaultLocation;

    /// <summary>Resolves the locations of the running editor.</summary>
    /// <param name="store">Where the last location is kept, or <see langword="null"/> for none.</param>
    /// <param name="documentsFolder">The OS documents folder, or an empty string when it has none.</param>
    /// <param name="homeFolder">The user's home folder, used when <paramref name="documentsFolder"/> is empty.</param>
    /// <returns>The locations, with the <c>NetPrints</c> folder under the documents folder as the default.</returns>
    public static ProjectLocations Resolve(IEditorStateStore? store, string documentsFolder, string homeFolder) =>
        new(store, string.IsNullOrEmpty(documentsFolder) ? homeFolder : documentsFolder, homeFolder);

    /// <summary>Creates the locations for the current user, in their documents folder.</summary>
    /// <param name="store">Where the last location is kept, or <see langword="null"/> for none.</param>
    /// <returns>The locations.</returns>
    public static ProjectLocations ForCurrentUser(IEditorStateStore? store)
    {
        string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return new ProjectLocations(store, string.IsNullOrEmpty(documents) ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) : documents);
    }

    /// <summary>Expands a leading <c>~</c> to the user's home folder; any other text is returned as it is, relative or not.</summary>
    /// <param name="location">The text the user typed.</param>
    /// <returns>The location with <c>~</c> expanded.</returns>
    public string Expand(string location)
    {
        ArgumentNullException.ThrowIfNull(location);
        string trimmed = location.Trim();
        if (trimmed == "~")
        {
            return homeFolder;
        }

        return trimmed.StartsWith("~/", StringComparison.Ordinal) || trimmed.StartsWith("~" + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            ? Path.Combine(homeFolder, trimmed[HomePrefixLength..])
            : location;
    }

    /// <summary>Remembers the location the user used, so the next New project and sample start there.</summary>
    /// <param name="location">The parent folder.</param>
    public void Remember(string location)
    {
        ArgumentException.ThrowIfNullOrEmpty(location);
        store?.Update(state => state with { NewProjectLocation = location });
    }
}
