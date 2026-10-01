#nullable enable
using System;
using System.Linq;
using NetPrints.Projects;

namespace NetPrints.Generation;

/// <summary>Builds the <see cref="GenerateRequest"/> the SDK's <c>NetPrintsGenerate</c> target would write, from a loaded project.</summary>
public static class GenerateRequestFactory
{
    /// <summary>Creates the request that generates every graph of <paramref name="snapshot"/> into its <c>.g.cs</c> file.</summary>
    /// <param name="snapshot">The loaded project.</param>
    /// <returns>The request: the project's namespace, profile, graphs (each paired with its generated file) and extension folders.</returns>
    public static GenerateRequest FromSnapshot(ProjectSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new GenerateRequest(
            snapshot.ProjectFilePath,
            snapshot.RootNamespace,
            snapshot.ProfileId,
            [.. snapshot.GraphFiles.Select(graph => new GraphJob(graph, ProjectFiles.GetGeneratedFilePath(graph)))],
            [.. snapshot.ExtensionFolders]);
    }
}
