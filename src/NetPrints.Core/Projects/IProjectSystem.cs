#nullable enable
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Core;

namespace NetPrints.Projects;

/// <summary>
/// Loads, edits, creates and builds SDK-style NetPrints projects (project-system.md §4). UI-free and
/// MSBuild-free at the abstraction level: the only implementation that talks to MSBuild directly is
/// <c>NetPrints.Workspace.MsBuildProjectSystem</c>. Every member is thread-safe; concurrent calls are
/// serialized internally (one MSBuild evaluation at a time).
/// </summary>
public interface IProjectSystem
{
    /// <summary>
    /// Loads <paramref name="projectFilePath"/>: restores if needed, evaluates it and opens it with
    /// <c>MSBuildWorkspace</c> (project-system.md §4). Idempotent; writes no files of its own (restore
    /// may write <c>obj/</c>).
    /// </summary>
    /// <param name="projectFilePath">Full path of the <c>.csproj</c> file to load.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A snapshot of the loaded project.</returns>
    /// <exception cref="ProjectSystemException">No .NET SDK is registered
    /// (<see cref="ProjectSystemException.NoSdkRegistered"/>), or evaluation failed
    /// (<see cref="ProjectSystemException.EvaluationFailed"/>).</exception>
    Task<ProjectSnapshot> LoadAsync(string projectFilePath, CancellationToken cancellationToken);

    /// <summary>
    /// Applies <paramref name="edits"/> to the project file in order, saves it once (preserving every
    /// part the edits do not touch) and reloads it.
    /// </summary>
    /// <param name="projectFilePath">Full path of the <c>.csproj</c> file to edit.</param>
    /// <param name="edits">Edits to apply, in order.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>A snapshot of the project after the edits were applied.</returns>
    /// <exception cref="System.ArgumentException"><see cref="ProjectEdit.RemoveReference"/> named a
    /// reference that is not <see cref="ProjectReferenceInfo.Editable"/>.</exception>
    Task<ProjectSnapshot> ApplyAsync(string projectFilePath, IReadOnlyList<ProjectEdit> edits, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new project from <paramref name="profile"/>'s template (New Project).
    /// </summary>
    /// <param name="directory">Directory the project is created in; must already exist.</param>
    /// <param name="projectName">Project name; the file is written as
    /// <c>&lt;directory&gt;/&lt;projectName&gt;.csproj</c>.</param>
    /// <param name="profile">Profile whose <see cref="IProjectProfile.ProjectTemplate"/> is written.</param>
    /// <param name="rootNamespace">Root namespace substituted into the template.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>Full path of the created <c>.csproj</c> file.</returns>
    /// <exception cref="System.IO.IOException">A <c>.csproj</c> already exists at that path; nothing
    /// is written.</exception>
    Task<string> CreateAsync(string directory, string projectName, IProjectProfile profile, string rootNamespace, CancellationToken cancellationToken);

    /// <summary>
    /// Builds the project out of process (<c>dotnet build</c>).
    /// </summary>
    /// <param name="projectFilePath">Full path of the <c>.csproj</c> file to build.</param>
    /// <param name="cancellationToken">Cancels the build; the build process tree is killed.</param>
    /// <returns>The build's outcome, messages and log.</returns>
    Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the command that runs the project's built output (<c>dotnet run --project &lt;p&gt;
    /// --no-build</c>), without building or running it.
    /// </summary>
    /// <param name="projectFilePath">Full path of the <c>.csproj</c> file to run.</param>
    /// <returns>The run command, ready to pass to an <see cref="IProcessRunner"/> or process
    /// launcher.</returns>
    ProcessStartRequest GetRunCommand(string projectFilePath);
}
