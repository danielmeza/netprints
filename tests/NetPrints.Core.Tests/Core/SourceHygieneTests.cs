using System;
using System.IO;
using System.Linq;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>FR-058: the product never resolves references from a hard-coded Windows install path.</summary>
    public class SourceHygieneTests
    {
        [Fact]
        public void NoSourceFileMentionsProgramFilesX86()
        {
            string src = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "src");
            string[] offenders = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
                .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
                .Where(path => File.ReadAllText(path).Contains("ProgramFilesX86", StringComparison.Ordinal))
                .Select(path => Path.GetRelativePath(src, path))
                .ToArray();

            Assert.Empty(offenders);
        }
    }
}
