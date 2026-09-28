using System;
using System.IO;
using System.Linq;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Architecture
{
    /// <summary>
    /// RL-T07 (release contract §5, research R19): the editor never resolves references from its own
    /// install layout or the running runtime's directory — only from what MSBuild resolves for the
    /// open project (reference packs, packages, project references), so a self-contained, non-single-file
    /// publish (T063 already deleted <c>ReferenceAssemblyResolver</c> and <c>CodeCompiler</c>, the P0
    /// fallback that enumerated <c>RuntimeEnvironment.GetRuntimeDirectory()</c>) changes nothing about
    /// what a graph compiles against.
    /// </summary>
    public class NoRuntimeDirectoryReferencesTests
    {
        [Fact]
        public void SourceNeverReferencesTheRuntimeDirectoryOrABundledReferenceAssemblySet()
        {
            string src = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src");
            string[] bannedTokens = ["RuntimeEnvironment.GetRuntimeDirectory", "ReferenceAssemblyResolver", "Basic.Reference.Assemblies"];

            string[] offenders = [.. Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(path => bannedTokens.Any(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal)))
                .Select(path => Path.GetRelativePath(src, path))];

            Assert.Empty(offenders);
        }

        [Fact]
        public void NoProjectReferencesBasicReferenceAssemblies()
        {
            string root = SampleProjectFactory.FindRepositoryRoot();
            string[] offenders = [.. new[] { "*.csproj", "*.props", "*.targets" }
                .SelectMany(pattern => Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories))
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}legacy{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(path => File.ReadAllText(path).Contains("Basic.Reference.Assemblies", StringComparison.Ordinal))
                .Select(path => Path.GetRelativePath(root, path))];

            Assert.Empty(offenders);
        }
    }
}
