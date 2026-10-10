namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>How the editor process is started: the project it opens, extra environment variables, and whether to wait for the project to load.</summary>
/// <param name="Project">The project file to open at startup, or <see langword="null"/> for the editor without a project.</param>
/// <param name="Environment">Environment variables added to the editor's, such as <c>NETPRINTS_STATE_DIR</c>.</param>
/// <param name="WaitForProject">Whether ready means the project is loaded; false when a dialog (recovery) holds the load until the test answers it.</param>
public sealed record EditorStart(string? Project = null, IReadOnlyDictionary<string, string>? Environment = null, bool WaitForProject = true);
