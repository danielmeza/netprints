using System;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Generation;
using NetPrints.Generator;
using Xunit;

namespace NetPrints.Tests.Projects
{
    /// <summary>
    /// R1-17/R1-20: <see cref="GenerateRequestFile.Parse"/>'s <see cref="FormatException"/> contract, and
    /// <c>NetPrints.Generator.Program.Main</c>'s exit codes for a malformed request (2) versus a request
    /// file that cannot even be read (also 2, not the internal-error 3).
    /// </summary>
    public sealed class GenerateRequestFileTests : IDisposable
    {
        private readonly string path = Path.Combine(Path.GetTempPath(), $"netprints-generate-{Guid.NewGuid():N}.rsp");

        public void Dispose()
        {
            try
            { File.Delete(path); }
            catch (IOException) { }
        }

        [Fact]
        public void ParsesAWellFormedRequest()
        {
            File.WriteAllText(path, "project=/work/App/App.csproj\nrootNamespace=App\nprofile=netprints.default\nextension=/ext\ngraph=/work/App/Program.netpc.json|/work/App/Program.netpc.g.cs\n");

            GenerateRequest request = GenerateRequestFile.Parse(path);

            Assert.Equal("/work/App/App.csproj", request.ProjectPath);
            Assert.Equal("App", request.RootNamespace);
            Assert.Equal("netprints.default", request.Profile);
            Assert.Equal(["/ext"], request.Extensions);
            GraphJob graph = Assert.Single(request.Graphs);
            Assert.Equal("/work/App/Program.netpc.json", graph.Input);
            Assert.Equal("/work/App/Program.netpc.g.cs", graph.Output);
        }

        [Theory]
        [InlineData("not-a-key-value-line")]
        [InlineData("project=/work/App/App.csproj\nprofile=netprints.default\ngraph=missing-pipe")]
        [InlineData("project=/work/App/App.csproj\nunknownKey=value")]
        public void ThrowsFormatExceptionForMalformedContent(string content)
        {
            File.WriteAllText(path, content);

            Assert.Throws<FormatException>(() => GenerateRequestFile.Parse(path));
        }

        [Fact]
        public void ThrowsFormatExceptionWhenProjectOrProfileIsMissing()
        {
            File.WriteAllText(path, "profile=netprints.default\n");

            Assert.Throws<FormatException>(() => GenerateRequestFile.Parse(path));
        }

        [Fact]
        public async Task ProgramMainReturnsBadRequestExitCodeForAMissingRequestFile()
        {
            // The rsp file was never written: File.ReadLines throws FileNotFoundException (an
            // IOException), which used to fall through to the internal-error path (exit 3).
            int exitCode = await Program.Main(["generate", path]);

            Assert.Equal(2, exitCode);
        }

        [Fact]
        public async Task ProgramMainReturnsBadRequestExitCodeForAMalformedRequestFile()
        {
            File.WriteAllText(path, "not-a-key-value-line");

            int exitCode = await Program.Main(["generate", path]);

            Assert.Equal(2, exitCode);
        }
    }
}
