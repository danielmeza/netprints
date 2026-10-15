using NetPrints.Core;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Lifecycle;

/// <summary>Which files of the open project have unsaved changes (FR-020).</summary>
public sealed class UnsavedChangesTracker
{
    private readonly ProjectSessionViewModel session;

    /// <summary>Creates the tracker of <paramref name="session"/>.</summary>
    /// <param name="session">The open project's session.</param>
    public UnsavedChangesTracker(ProjectSessionViewModel session)
    {
        ArgumentNullException.ThrowIfNull(session);
        this.session = session;
    }

    /// <summary>Gets the unsaved files: the class graph files in project order, then the project file when a project-level change is pending.</summary>
    public IReadOnlyList<UnsavedFile> UnsavedFiles
    {
        get
        {
            List<UnsavedFile> files =
            [
                .. session.Project.Classes
                    .Where(cls => cls.IsDirty)
                    .Select(cls => new UnsavedFile(session.ClassPathOf(cls), UnsavedFileKind.Class, cls.Name)),
            ];
            if (IsProjectFileUnsaved)
            {
                files.Add(new UnsavedFile(Path.GetFileName(session.ProjectFilePath), UnsavedFileKind.Project, session.Project.Name));
            }

            return files;
        }
    }

    /// <summary>Gets whether any file is unsaved.</summary>
    public bool HasUnsavedFiles => IsProjectFileUnsaved || session.Project.Classes.Any(cls => cls.IsDirty);

    /// <summary>Gets whether a project-level change is pending.</summary>
    public bool IsProjectFileUnsaved { get; private set; }

    /// <summary>Tells whether a class graph file is unsaved.</summary>
    /// <param name="cls">A class of the project.</param>
    /// <returns><see langword="true"/> when the class has unsaved changes.</returns>
    public bool IsUnsaved(ClassGraph cls)
    {
        ArgumentNullException.ThrowIfNull(cls);
        return cls.IsDirty;
    }

    /// <summary>Records a project-level change that is not written yet.</summary>
    public void MarkProjectChangePending()
    {
        IsProjectFileUnsaved = true;
    }

    /// <summary>Records that the project file was written.</summary>
    public void ClearProjectChangePending()
    {
        IsProjectFileUnsaved = false;
    }

    /// <summary>Counts the files a save of <paramref name="only"/> (or of everything, when null) writes.</summary>
    internal int CountUnsaved(ClassGraph? only) =>
        only is null ? UnsavedFiles.Count : only.IsDirty ? 1 : 0;
}
