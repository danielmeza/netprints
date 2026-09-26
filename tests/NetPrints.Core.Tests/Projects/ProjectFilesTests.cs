using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Tests.Projects
{
    /// <summary>
    /// <see cref="ProjectFiles.EnsureGitAttributesAsync"/> (project-system.md §1.1): the low-level
    /// helper behind the <c>.gitattributes</c> part of PS-T15, exercised directly here (PS-T15 itself,
    /// through <c>IProjectSystem.CreateAsync</c> and <c>ProjectConverter</c>, is covered in T053/T054).
    /// </summary>
    public class ProjectFilesTests
    {
        [Fact]
        public async Task CreatesFileWithExactlyTheTwoLinesWhenMissing()
        {
            string directory = Directory.CreateTempSubdirectory("netprints-gitattributes-").FullName;
            try
            {
                byte[]? previous = await ProjectFiles.EnsureGitAttributesAsync(directory, TestContext.Current.CancellationToken);

                Assert.Null(previous);
                string content = await File.ReadAllTextAsync(Path.Combine(directory, ".gitattributes"), TestContext.Current.CancellationToken);
                Assert.Equal("*.netpc.json text eol=lf\n*.netpc.g.cs text eol=lf\n", content);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task AppendsOnlyTheMissingLineWithoutDisturbingExistingContent()
        {
            string directory = Directory.CreateTempSubdirectory("netprints-gitattributes-").FullName;
            try
            {
                string path = Path.Combine(directory, ".gitattributes");
                // No trailing newline, and a line unrelated to NetPrints, on purpose.
                await File.WriteAllTextAsync(path, "*.netpc.json text eol=lf\n*.png binary", TestContext.Current.CancellationToken);

                byte[]? previous = await ProjectFiles.EnsureGitAttributesAsync(directory, TestContext.Current.CancellationToken);

                Assert.NotNull(previous);
                Assert.Equal("*.netpc.json text eol=lf\n*.png binary", Encoding.UTF8.GetString(previous));
                string content = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
                Assert.Equal("*.netpc.json text eol=lf\n*.png binary\n*.netpc.g.cs text eol=lf\n", content);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [Fact]
        public async Task RunningTwiceAppendsNothingAndLeavesTheFileUnchanged()
        {
            string directory = Directory.CreateTempSubdirectory("netprints-gitattributes-").FullName;
            try
            {
                await ProjectFiles.EnsureGitAttributesAsync(directory, TestContext.Current.CancellationToken);
                string path = Path.Combine(directory, ".gitattributes");
                byte[] afterFirst = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
                DateTime writeTimeAfterFirst = File.GetLastWriteTimeUtc(path);

                byte[]? previous = await ProjectFiles.EnsureGitAttributesAsync(directory, TestContext.Current.CancellationToken);

                Assert.Equal(afterFirst, previous);
                byte[] afterSecond = await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken);
                Assert.Equal(afterFirst, afterSecond);
                Assert.Equal(writeTimeAfterFirst, File.GetLastWriteTimeUtc(path));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
