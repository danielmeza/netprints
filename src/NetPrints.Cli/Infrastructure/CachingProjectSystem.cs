using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Projects;

namespace NetPrints.Cli.Infrastructure;

/// <summary>
/// Answers <see cref="LoadAsync"/> once per project path for the life of one command run, so a project the command already loaded
/// is not evaluated and design-time built a second time by the code it hands the project system to.
/// </summary>
internal sealed class CachingProjectSystem(IProjectSystem inner) : IProjectSystem
{
    private readonly Dictionary<string, Task<ProjectSnapshot>> _loads = new(PathComparer);

    private readonly Lock _gate = new();

    private static StringComparer PathComparer => OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <inheritdoc/>
    public async Task<ProjectSnapshot> LoadAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectFilePath);
        string key = Path.GetFullPath(projectFilePath);
        Task<ProjectSnapshot>? load;
        lock (_gate)
        {
            if (!_loads.TryGetValue(key, out load))
            {
                load = inner.LoadAsync(projectFilePath, cancellationToken);
                _loads[key] = load;
            }
        }

        try
        {
            return await load.ConfigureAwait(false);
        }
        catch
        {
            lock (_gate)
            {
                _loads.Remove(key);
            }

            throw;
        }
    }

    /// <inheritdoc/>
    public Task<ProjectSnapshot> ApplyAsync(string projectFilePath, IReadOnlyList<ProjectEdit> edits, CancellationToken cancellationToken)
    {
        Forget(projectFilePath);
        return inner.ApplyAsync(projectFilePath, edits, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<string> CreateAsync(string directory, string projectName, IProjectProfile profile, string rootNamespace, CancellationToken cancellationToken) =>
        inner.CreateAsync(directory, projectName, profile, rootNamespace, cancellationToken);

    /// <inheritdoc/>
    public Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        Forget(projectFilePath);
        return inner.BuildAsync(projectFilePath, cancellationToken);
    }

    /// <inheritdoc/>
    public ProcessStartRequest GetRunCommand(string projectFilePath) => inner.GetRunCommand(projectFilePath);

    private void Forget(string projectFilePath)
    {
        lock (_gate)
        {
            _loads.Remove(Path.GetFullPath(projectFilePath));
        }
    }
}
