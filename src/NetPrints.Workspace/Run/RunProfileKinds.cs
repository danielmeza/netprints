using System;

namespace NetPrints.Workspace.Run;

/// <summary>The <c>commandName</c> values NetPrints can run. The table is internal, not an extension point.</summary>
public static class RunProfileKinds
{
    /// <summary>The <c>Project</c> kind: runs the project itself, as <c>dotnet run</c> does.</summary>
    public const string Project = "Project";

    /// <summary>The <c>Executable</c> kind: runs the program named by <c>executablePath</c>.</summary>
    public const string Executable = "Executable";

    /// <summary>Whether a <c>commandName</c> is a kind NetPrints can run. The comparison is ordinal.</summary>
    /// <param name="commandName">The profile's <c>commandName</c>.</param>
    /// <returns><see langword="true"/> for <c>Project</c> and <c>Executable</c>.</returns>
    public static bool IsRunnable(string commandName) =>
        string.Equals(commandName, Project, StringComparison.Ordinal)
        || string.Equals(commandName, Executable, StringComparison.Ordinal);
}
