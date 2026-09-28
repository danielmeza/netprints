using NetPrints.Core;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Serialization;

namespace NetPrints.Editor.Tests;

/// <summary>Helpers for temporary directories and the checked-in sample.</summary>
public static class TestPaths
{
    public static string CreateTempDirectory()
    {
        string dir = Path.Combine(Path.GetTempPath(), "netprints-editor-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Copies the checked-in <c>samples/HelloWorld</c> (linked into the test output) to a new temp
    /// directory (T059/T062a switched the editor to <c>.csproj</c>, research.md R21).
    /// </summary>
    /// <returns>The copied project's <c>.csproj</c> path.</returns>
    public static string CopyHelloWorldSample()
    {
        string source = Path.Combine(AppContext.BaseDirectory, "samples", "HelloWorld");
        string target = CreateTempDirectory();
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        return Path.Combine(target, "HelloWorld.csproj");
    }

    /// <summary>
    /// Copies <see cref="CopyHelloWorldSample"/> and loads it through a real
    /// <see cref="ProjectPersistence"/> (a <see cref="FakeProjectSystem"/> supplies the snapshot by
    /// scanning the copied files, no real MSBuild involved).
    /// </summary>
    public static async Task<Project> LoadHelloWorldCopyAsync(CancellationToken cancellationToken)
    {
        string csprojPath = CopyHelloWorldSample();
        ProjectPersistence persistence = TestEditor.CreatePersistence(new FakeProjectSystem());

        ProjectLoadResult loaded = await persistence.LoadAsync(csprojPath, cancellationToken);
        Assert.Empty(loaded.Issues);
        return loaded.Project;
    }

    /// <summary>Walks up from <see cref="AppContext.BaseDirectory"/> to find the checked-out repository root.</summary>
    /// <returns>The repository root directory (the one containing <c>NetPrints.slnx</c>).</returns>
    /// <exception cref="InvalidOperationException">No <c>NetPrints.slnx</c> was found above the running tests' output directory.</exception>
    public static string FindRepositoryRoot()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "NetPrints.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException($"Could not find the repository root (NetPrints.slnx) above '{AppContext.BaseDirectory}'.");
    }

    /// <summary>
    /// Writes the same in-repo <c>NetPrints.Sdk</c> development-mode files as <c>samples/</c>
    /// (project-system.md §2.1) into <paramref name="targetDirectory"/>, with absolute paths so they
    /// resolve from a temp copy of a sample (used by <see cref="Hosting.ProjectOpenPerformanceTests"/>
    /// and <see cref="Hosting.ProjectCheckTests"/>).
    /// </summary>
    public static void WriteLocalSdkLayout(string targetDirectory)
    {
        string repositoryRoot = FindRepositoryRoot();
        string generatorPath = Path.Combine(repositoryRoot, "src", "NetPrints.Generator", "bin",
            DetectConfiguration(), "net10.0", "NetPrints.Generator.dll");
        string sdkPropsPath = Path.Combine(repositoryRoot, "src", "NetPrints.Sdk", "build", "NetPrints.Sdk.props");
        string sdkTargetsPath = Path.Combine(repositoryRoot, "src", "NetPrints.Sdk", "build", "NetPrints.Sdk.targets");

        File.WriteAllText(Path.Combine(targetDirectory, "Directory.Build.props"), $"""
            <Project>
              <PropertyGroup>
                <NetPrintsUseLocalSdk>true</NetPrintsUseLocalSdk>
                <NetPrintsGeneratorPath>{generatorPath}</NetPrintsGeneratorPath>
              </PropertyGroup>

              <Import Project="{sdkPropsPath}" />
            </Project>

            """);

        File.WriteAllText(Path.Combine(targetDirectory, "Directory.Build.targets"), $"""
            <Project>
              <Import Project="{sdkTargetsPath}" />
            </Project>

            """);

        File.WriteAllText(Path.Combine(targetDirectory, "Directory.Packages.props"), """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
              </PropertyGroup>
            </Project>

            """);
    }

    /// <summary>
    /// The build configuration ("Debug" or "Release") of the currently running test binaries, read
    /// from their own output directory (<c>bin/&lt;configuration&gt;/net10.0/</c>) so
    /// <see cref="WriteLocalSdkLayout"/>'s generator path matches whichever configuration built it.
    /// </summary>
    /// <returns>The detected configuration name, or <c>"Release"</c> if it could not be determined.</returns>
    private static string DetectConfiguration()
    {
        string[] segments = AppContext.BaseDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        int frameworkIndex = Array.LastIndexOf(segments, "net10.0");
        return frameworkIndex > 0 ? segments[frameworkIndex - 1] : "Release";
    }

    public static void TryDelete(string? path)
    {
        if (path is null)
        {
            return;
        }

        try
        {
            string dir = Directory.Exists(path) ? path : Path.GetDirectoryName(path) ?? path;
            Directory.Delete(dir, true);
        }
        catch (IOException)
        {
        }
    }
}
