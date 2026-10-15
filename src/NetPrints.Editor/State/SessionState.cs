namespace NetPrints.Editor.State;

/// <summary>The content of <c>sessions/&lt;project-key&gt;.json</c>: what one user had open in one project.</summary>
/// <param name="SchemaVersion">The schema version.</param>
/// <param name="ProjectPath">The project file's path.</param>
/// <param name="OpenDocuments">The open documents, in tab order, as document ids.</param>
/// <param name="ActiveDocument">The active document's id, if any.</param>
/// <param name="Viewports">Each graph's viewport by document id.</param>
public sealed record SessionState(
    int SchemaVersion,
    string ProjectPath,
    IReadOnlyList<string> OpenDocuments,
    string? ActiveDocument,
    IReadOnlyDictionary<string, ViewportState> Viewports) : IStateFile;

/// <summary>A graph's viewport.</summary>
/// <param name="X">Location, x.</param>
/// <param name="Y">Location, y.</param>
/// <param name="Zoom">Zoom factor.</param>
public sealed record ViewportState(double X, double Y, double Zoom);
