namespace NetPrints.Testing;

/// <summary>
/// Writes the same in-repo <c>NetPrints.Sdk</c> development-mode files as <c>samples/</c>
/// (project-system.md §2.1) into an arbitrary directory, with absolute paths instead of
/// <c>samples/</c>' repository-relative ones: a temp copy of a sample has no fixed relationship to
/// the repository root, so a relative <c>../src/...</c> import would not resolve. Shared by every
/// test assembly that builds a temp copy of a sample against the repository's own SDK
/// (Core.Tests, Editor.Tests, Editor.UITests, Desktop.E2ETests): consolidated from three
/// near-identical copies (batch D2).
/// </summary>
public static class LocalSdkLayout
{
    /// <summary>
    /// Writes <c>Directory.Build.props</c>, <c>Directory.Build.targets</c> and
    /// <c>Directory.Packages.props</c> into <paramref name="directory"/>.
    /// </summary>
    /// <param name="directory">Directory to write the files into (created if it does not exist). A
    /// project built here (or in a subdirectory of it) picks them up the same way a real sample
    /// under <c>samples/</c> does.</param>
    public static void Write(string directory)
    {
        ArgumentException.ThrowIfNullOrEmpty(directory);
        Directory.CreateDirectory(directory);

        string repositoryRoot = FindRepositoryRoot();
        string generatorPath = Path.Combine(repositoryRoot, "src", "NetPrints.Generator", "bin",
            DetectConfiguration(), "net10.0", "NetPrints.Generator.dll");
        string sdkPropsPath = Path.Combine(repositoryRoot, "src", "NetPrints.Sdk", "build", "NetPrints.Sdk.props");
        string sdkTargetsPath = Path.Combine(repositoryRoot, "src", "NetPrints.Sdk", "build", "NetPrints.Sdk.targets");

        File.WriteAllText(Path.Combine(directory, "Directory.Build.props"), $"""
            <Project>
              <PropertyGroup>
                <NetPrintsUseLocalSdk>true</NetPrintsUseLocalSdk>
                <NetPrintsGeneratorPath>{generatorPath}</NetPrintsGeneratorPath>
              </PropertyGroup>

              <Import Project="{sdkPropsPath}" />
            </Project>

            """);

        File.WriteAllText(Path.Combine(directory, "Directory.Build.targets"), $"""
            <Project>
              <Import Project="{sdkTargetsPath}" />
            </Project>

            """);

        File.WriteAllText(Path.Combine(directory, "Directory.Packages.props"), """
            <Project>
              <PropertyGroup>
                <ManagePackageVersionsCentrally>false</ManagePackageVersionsCentrally>
              </PropertyGroup>
            </Project>

            """);
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
    /// The build configuration ("Debug" or "Release") of the currently running test binaries, read
    /// from their own output directory (<c>bin/&lt;configuration&gt;/net10.0/</c>) so the generator
    /// path this writes matches whichever configuration built it.
    /// </summary>
    /// <returns>The detected configuration name, or <c>"Release"</c> if it could not be determined.</returns>
    public static string DetectConfiguration()
    {
        string[] segments = AppContext.BaseDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        int frameworkIndex = Array.LastIndexOf(segments, "net10.0");
        return frameworkIndex > 0 ? segments[frameworkIndex - 1] : "Release";
    }
}
