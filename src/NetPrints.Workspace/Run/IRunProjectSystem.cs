using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Projects;

namespace NetPrints.Workspace.Run;

/// <summary>The part of a project system that builds and evaluates a project in a chosen configuration.</summary>
public interface IRunProjectSystem
{
    /// <summary>Builds the project as <c>IProjectSystem.BuildAsync</c> does, in <paramref name="configuration"/> (<c>-c</c>).</summary>
    /// <param name="projectFilePath">The full path of the project file.</param>
    /// <param name="configuration">The build configuration.</param>
    /// <param name="cancellationToken">Cancels the build.</param>
    /// <returns>The build result.</returns>
    Task<BuildResult> BuildAsync(string projectFilePath, string configuration, CancellationToken cancellationToken);

    /// <summary>
    /// Evaluates the project in process with the global property <c>Configuration</c> (and its first framework when it multi-targets).
    /// </summary>
    /// <param name="projectFilePath">The full path of the project file.</param>
    /// <param name="configuration">The configuration to evaluate in.</param>
    /// <param name="names">The property names to read.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>The names the project defines with their values, keyed case-insensitively; an undefined name is absent.</returns>
    Task<IReadOnlyDictionary<string, string>> EvaluatePropertiesAsync(
        string projectFilePath,
        string configuration,
        IReadOnlyList<string> names,
        CancellationToken cancellationToken);
}
