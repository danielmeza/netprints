using System;
using System.IO;
using NetPrints.Tests.Samples;

namespace NetPrints.Tests.Projects
{
    /// <summary>
    /// Writes the same in-repo <c>NetPrints.Sdk</c> development-mode files as <c>samples/</c>
    /// (project-system.md §2.1) into an arbitrary directory, with absolute paths instead of
    /// <c>samples/</c>' repository-relative ones: a temp directory used by a test has no fixed
    /// relationship to the repository root, so a relative <c>../src/...</c> import would not resolve.
    /// </summary>
    public static class LocalSdkLayout
    {
        /// <summary>
        /// Writes <c>Directory.Build.props</c>, <c>Directory.Build.targets</c> and
        /// <c>Directory.Packages.props</c> into <paramref name="directory"/>, pointing
        /// <c>NetPrintsGeneratorPath</c> at this repository's own built
        /// <c>src/NetPrints.Generator/bin/&lt;configuration&gt;/net10.0/NetPrints.Generator.dll</c> (the
        /// configuration the running test binaries were themselves built with) and importing
        /// <c>NetPrints.Sdk.props</c>/<c>.targets</c> from <c>src/NetPrints.Sdk/build/</c> by absolute
        /// path.
        /// </summary>
        /// <param name="directory">Directory to write the files into (created if it does not exist).
        /// A project built here (or in a subdirectory of it) picks them up the same way a real sample
        /// under <c>samples/</c> does.</param>
        public static void Write(string directory)
        {
            ArgumentException.ThrowIfNullOrEmpty(directory);
            Directory.CreateDirectory(directory);

            string repositoryRoot = SampleProjectFactory.FindRepositoryRoot();
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

        /// <summary>
        /// The build configuration ("Debug" or "Release") of the currently running test binaries,
        /// read from their own output directory (<c>bin/&lt;configuration&gt;/net10.0/</c>) so the
        /// generator path this writes matches whichever configuration built it.
        /// </summary>
        /// <returns>The detected configuration name, or <c>"Release"</c> if it could not be
        /// determined.</returns>
        private static string DetectConfiguration()
        {
            string[] segments = AppContext.BaseDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            int frameworkIndex = Array.LastIndexOf(segments, "net10.0");
            return frameworkIndex > 0 ? segments[frameworkIndex - 1] : "Release";
        }
    }
}
