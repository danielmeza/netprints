using System.IO;
using NetPrints.Tests.Projects;
using NetPrints.Tests.Samples;

namespace NetPrints.Tests.Extensibility;

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
