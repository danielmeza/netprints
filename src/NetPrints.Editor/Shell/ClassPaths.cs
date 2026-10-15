using NetPrints.Core;

namespace NetPrints.Editor.Shell;

internal static class ClassPaths
{
    /// <summary>Gets the class path a class has now, as <see cref="ProjectSessionViewModel.ClassPathOf"/> would fix it for a session that had not seen the class.</summary>
    /// <param name="project">The project that holds the class.</param>
    /// <param name="cls">A class of the project.</param>
    /// <returns>The path relative to the project, with <c>/</c> separators.</returns>
    internal static string Of(Project project, ClassGraph cls)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(cls);
        string directory = Path.GetDirectoryName(Path.GetFullPath(project.Path)) ?? "";
        return Path.GetRelativePath(directory, Path.GetFullPath(project.GetGraphFilePath(cls))).Replace('\\', '/');
    }
}
