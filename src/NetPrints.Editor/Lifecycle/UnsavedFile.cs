namespace NetPrints.Editor.Lifecycle;

/// <summary>What an <see cref="UnsavedFile"/> is the file of.</summary>
public enum UnsavedFileKind
{
    /// <summary>A class graph file.</summary>
    Class,

    /// <summary>The project file.</summary>
    Project,
}

/// <summary>A file of the open project with changes not written yet.</summary>
/// <param name="Path">The class path of a class graph file (relative to the project, <c>/</c> separators), or the project file's name.</param>
/// <param name="Kind">What the file is.</param>
/// <param name="DisplayName">The name the unsaved changes dialog lists: the class name, or the project name.</param>
public sealed record UnsavedFile(string Path, UnsavedFileKind Kind, string DisplayName);
