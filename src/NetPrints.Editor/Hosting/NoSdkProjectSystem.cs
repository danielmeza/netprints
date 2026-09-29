using NetPrints.Core;
using NetPrints.Projects;

namespace NetPrints.Editor.Hosting;

/// <summary>
/// <see cref="IProjectSystem"/> used when no .NET SDK could be registered
/// (<c>MsBuildRegistration.EnsureRegistered</c> returned <see langword="false"/>,
/// <see cref="EditorHostServices.MsBuildAvailable"/>, project-system.md §4): every member throws
/// <see cref="ProjectSystemException"/> with <see cref="ProjectSystemException.NoSdkRegistered"/>
/// (<c>NPW001</c>) instead of touching MSBuild, so the editor still starts and reports the problem
/// through the usual error dialog the first time a project operation is attempted (PS-T13).
/// </summary>
public sealed class NoSdkProjectSystem : IProjectSystem
{
    /// <summary>
    /// Reported through every thrown <see cref="ProjectSystemException"/>; also used, unthrown, by
    /// <c>NetPrints.Desktop.ProjectCheck</c>'s own registration-failure message (release contract §5).
    /// </summary>
    public const string Message = "No .NET SDK could be found; projects cannot be opened, created or built.";

    /// <inheritdoc/>
    public Task<ProjectSnapshot> LoadAsync(string projectFilePath, CancellationToken cancellationToken) => throw NoSdk();

    /// <inheritdoc/>
    public Task<ProjectSnapshot> ApplyAsync(string projectFilePath, IReadOnlyList<ProjectEdit> edits, CancellationToken cancellationToken) => throw NoSdk();

    /// <inheritdoc/>
    public Task<string> CreateAsync(string directory, string projectName, IProjectProfile profile, string rootNamespace, CancellationToken cancellationToken) => throw NoSdk();

    /// <inheritdoc/>
    public Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken) => throw NoSdk();

    /// <inheritdoc/>
    public ProcessStartRequest GetRunCommand(string projectFilePath) => throw NoSdk();

    private static ProjectSystemException NoSdk() => new(ProjectSystemException.NoSdkRegistered, Message);
}
