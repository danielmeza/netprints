using NetPrints.Core;
using NetPrints.Extensibility.Testing;
using NetPrints.Generation;
using NetPrints.Projects;

namespace NetPrints.Testing.Extensions;

/// <summary>Drives the generator with what a kit <see cref="ExtensionHarness"/> loaded. Test support; not packaged.</summary>
public static class ExtensionHarnessGeneration
{
    private const string DocumentNameSuffix = ".netpc.json";
    private const string GeneratedSuffix = ".g.cs";

    /// <summary>Generates the <c>.netpc.g.cs</c> of every <c>.netpc.json</c> under <paramref name="projectDirectory"/>.</summary>
    /// <param name="harness">The harness whose registry the generator uses.</param>
    /// <param name="projectDirectory">The directory to scan recursively; it names the project.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>One result per graph, in ordinal path order.</returns>
    public static async Task<IReadOnlyList<GeneratedFileResult>> GenerateAsync(this ExtensionHarness harness, string projectDirectory, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(harness);
        ArgumentException.ThrowIfNullOrEmpty(projectDirectory);
        string name = new DirectoryInfo(projectDirectory).Name;
        GraphJob[] jobs =
        [
            .. Directory.EnumerateFiles(projectDirectory, "*" + DocumentNameSuffix, SearchOption.AllDirectories)
                .Order(StringComparer.Ordinal)
                .Select(path => new GraphJob(path, Path.ChangeExtension(path, null) + GeneratedSuffix)),
        ];
        var request = new GenerateRequest(Path.Combine(projectDirectory, name + ".csproj"), name, DefaultProjectProfile.ProfileId, jobs, []);
        return await GraphCodeGenerator.Create(harness.Registry).GenerateAsync(request, cancellationToken);
    }
}
