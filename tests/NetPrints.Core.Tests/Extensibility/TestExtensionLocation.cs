using System.IO;
using NetPrints.Testing;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Extensibility;

/// <summary>
/// Serializes the tests that load the real <c>NetPrints.TestExtension</c> assembly through their own
/// <c>AssemblyLoadContext</c> (batch D4): concurrent <see cref="System.Runtime.Loader.AssemblyDependencyResolver"/>
/// construction from several threads at once corrupted the process (an AccessViolationException
/// surfacing in unrelated code, CI run 36413425390). One collection, not the whole assembly, so the
/// rest of Core.Tests keeps its parallelism.
/// </summary>
[CollectionDefinition(nameof(RealExtensionLoadCollection), DisableParallelization = true)]
public sealed class RealExtensionLoadCollection;

/// <summary>Where the built <c>NetPrints.TestExtension</c> asset (T071) lands for the running configuration.</summary>
public static class TestExtensionLocation
{
    /// <summary>The extension's output folder: its dll, its manifest and nothing the host already supplies.</summary>
    public static string Folder { get; } = Path.Combine(
        SampleProjectFactory.FindRepositoryRoot(), "tests", "NetPrints.TestExtension", "bin",
        LocalSdkLayout.DetectConfiguration(), "extensions", "netprints.test");

    /// <summary>Copies <see cref="Folder"/> to <c>&lt;destinationRoot&gt;/&lt;folderName&gt;</c>.</summary>
    /// <param name="destinationRoot">The directory that will hold the copy, for use as a search directory.</param>
    /// <param name="folderName">The name of the copied folder.</param>
    /// <returns>The path of the copy.</returns>
    public static string CopyTo(string destinationRoot, string folderName = "netprints.test")
    {
        string destination = Path.Combine(destinationRoot, folderName);
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(Folder))
        {
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        return destination;
    }
}
