namespace NetPrints.Editor.Shell;

/// <summary>What a <see cref="DocumentId"/> identifies.</summary>
public enum DocumentKind
{
    /// <summary>A graph of a class: a method, constructor, event graph or the class graph.</summary>
    Graph,

    /// <summary>The start page, shown with no project open.</summary>
    StartPage,

    /// <summary>The project settings document.</summary>
    ProjectSettings,
}

/// <summary>
/// The identity of a shell document, with value equality. Serialised as <c>graph:&lt;classPath&gt;#&lt;graphKey&gt;</c>
/// (the key being <c>method:&lt;id&gt;</c>, <c>ctor:&lt;id&gt;</c>, <c>event:&lt;id&gt;</c> or <c>class</c>),
/// <c>start</c> or <c>project-settings</c> (contracts/shell.md §2).
/// </summary>
public sealed record DocumentId
{
    private const string GraphPrefix = "graph:";
    private const string StartText = "start";
    private const string ProjectSettingsText = "project-settings";
    /// <summary>The prefix of the graph key of a method; the method's id follows.</summary>
    public const string MethodKeyPrefix = "method:";

    /// <summary>The prefix of the graph key of a constructor; its id follows.</summary>
    public const string ConstructorKeyPrefix = "ctor:";

    /// <summary>The prefix of the graph key of an event graph; its id follows.</summary>
    public const string EventKeyPrefix = "event:";

    /// <summary>The graph key of the class graph.</summary>
    public const string ClassGraphKey = "class";

    private static readonly string[] KeyPrefixes = [MethodKeyPrefix, ConstructorKeyPrefix, EventKeyPrefix];

    private DocumentId(DocumentKind kind, string? classPath, string? graphKey)
    {
        Kind = kind;
        ClassPath = classPath;
        GraphKey = graphKey;
    }

    /// <summary>Gets the start page id.</summary>
    public static DocumentId StartPage { get; } = new(DocumentKind.StartPage, null, null);

    /// <summary>Gets the project settings id.</summary>
    public static DocumentId ProjectSettings { get; } = new(DocumentKind.ProjectSettings, null, null);

    /// <summary>Gets what the id identifies.</summary>
    public DocumentKind Kind { get; }

    /// <summary>Gets the class file path relative to the project, for a graph; otherwise null.</summary>
    public string? ClassPath { get; }

    /// <summary>Gets the graph key (<c>method:id</c>, <c>ctor:id</c>, <c>event:id</c> or <c>class</c>), for a graph; otherwise null.</summary>
    public string? GraphKey { get; }

    /// <summary>Creates the id of a graph document.</summary>
    /// <param name="classPath">The class file path relative to the project; non-empty, may contain <c>#</c>.</param>
    /// <param name="graphKey">A valid graph key.</param>
    /// <returns>The id.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">The path is empty or the key is not valid.</exception>
    public static DocumentId Graph(string classPath, string graphKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(classPath);
        ArgumentNullException.ThrowIfNull(graphKey);
        if (!IsValidGraphKey(graphKey))
        {
            throw new ArgumentException($"Invalid graph document: '{classPath}#{graphKey}'.", nameof(graphKey));
        }

        return new DocumentId(DocumentKind.Graph, classPath, graphKey);
    }

    /// <summary>Parses the text form produced by <see cref="ToString"/>.</summary>
    /// <param name="text">The text; may be null.</param>
    /// <param name="id">The parsed id on success.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> is a well-formed id.</returns>
    public static bool TryParse(string? text, out DocumentId id)
    {
        id = StartPage;
        switch (text)
        {
            case StartText:
                return true;
            case ProjectSettingsText:
                id = ProjectSettings;
                return true;
            case null:
                return false;
        }

        if (!text.StartsWith(GraphPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string rest = text[GraphPrefix.Length..];
        int hash = rest.LastIndexOf('#');
        if (hash <= 0)
        {
            return false;
        }

        string classPath = rest[..hash];
        string graphKey = rest[(hash + 1)..];
        if (!IsValidGraphKey(graphKey))
        {
            return false;
        }

        id = new DocumentId(DocumentKind.Graph, classPath, graphKey);
        return true;
    }

    /// <summary>Gets the text form.</summary>
    /// <returns>The serialised id.</returns>
    public override string ToString() => Kind switch
    {
        DocumentKind.StartPage => StartText,
        DocumentKind.ProjectSettings => ProjectSettingsText,
        _ => $"{GraphPrefix}{ClassPath}#{GraphKey}",
    };

    private static bool IsValidGraphKey(string key) =>
        key == ClassGraphKey
        || KeyPrefixes.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal) && key.Length > prefix.Length && !key.Contains('#', StringComparison.Ordinal));
}
