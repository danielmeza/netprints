using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Projects;
using NetPrints.Workspace;
using Xunit;

namespace NetPrints.Tests.Projects
{
    /// <summary>
    /// FR-070, FR-071, US9 scenarios 1 and 2, ADR-0038: the project system reads <c>LangVersion</c>,
    /// <c>MaxSupportedLangVersion</c> and the target frameworks from the evaluation and resolves the C# target, over the six
    /// fixtures in <c>tests/Fixtures/CSharpTarget</c>.
    /// </summary>
    public class CSharpTargetWorkspaceTests
    {
        private static readonly CSharpLanguageVersion Cs10 = new(10, 0);

        private static readonly CSharpLanguageVersion Cs12 = new(12, 0);

        private static readonly CSharpLanguageVersion Cs14 = new(14, 0);

        private static async Task<ProjectSnapshot> LoadFixtureAsync(string name)
        {
            string source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CSharpTarget", name);
            string directory = Directory.CreateTempSubdirectory($"netprints-cstarget-{name}-").FullName;
            try
            {
                foreach (string file in Directory.EnumerateFiles(source))
                {
                    File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
                }

                var system = new MsBuildProjectSystem(new ProjectSystemOptions([], "9.9.9-test"), new ProcessRunner(),
                    NullLogger<MsBuildProjectSystem>.Instance);
                return await system.LoadAsync(Path.Combine(directory, $"{name}.csproj"), TestContext.Current.CancellationToken);
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static IReadOnlyList<string> TargetCodes(ProjectSnapshot snapshot) =>
        [
            .. snapshot.Messages.Select(message => message.Code)
                .Where(code => code is ProjectMessage.LanguageVersionResolved or ProjectMessage.LanguageVersionNewerThanFramework or ProjectMessage.LanguageVersionUnknown),
        ];

        [Fact]
        public async Task NoLangVersionUsesTheFrameworkDefaultWithoutAMessage()
        {
            ProjectSnapshot snapshot = await LoadFixtureAsync("NoLangVersion");

            Assert.Equal(Cs14, snapshot.CSharpTarget.Version);
            Assert.Equal("14.0", snapshot.CSharpTarget.Specified);
            Assert.Equal(["net10.0"], snapshot.CSharpTarget.TargetFrameworks);
            Assert.Empty(TargetCodes(snapshot));
        }

        [Fact]
        public async Task LatestResolvesWithAHintToPin()
        {
            ProjectSnapshot snapshot = await LoadFixtureAsync("Latest");

            Assert.Equal(Cs14, snapshot.CSharpTarget.Version);
            Assert.Equal("latest", snapshot.CSharpTarget.Specified);
            Assert.Equal([ProjectMessage.LanguageVersionResolved], TargetCodes(snapshot));
            Assert.All(snapshot.Messages.Where(message => message.Code == ProjectMessage.LanguageVersionResolved),
                message => Assert.Equal(ProjectMessageSeverity.Info, message.Severity));
        }

        [Fact]
        public async Task PreviewResolvesWithAHintToPin()
        {
            ProjectSnapshot snapshot = await LoadFixtureAsync("Preview");

            Assert.Equal(Cs14, snapshot.CSharpTarget.Version);
            Assert.Equal("preview", snapshot.CSharpTarget.Specified);
            Assert.Equal([ProjectMessage.LanguageVersionResolved], TargetCodes(snapshot));
        }

        [Fact]
        public async Task APinnedOlderVersionIsKept()
        {
            ProjectSnapshot snapshot = await LoadFixtureAsync("Pinned10");

            Assert.Equal(Cs10, snapshot.CSharpTarget.Version);
            Assert.Equal("10.0", snapshot.CSharpTarget.Specified);
            Assert.Empty(TargetCodes(snapshot));
        }

        [Fact]
        public async Task AVersionNewerThanTheEditorKnowsUsesTheEditorsMaximum()
        {
            ProjectSnapshot snapshot = await LoadFixtureAsync("Unknown16");

            Assert.Equal(CSharpLanguageVersion.MaxKnown, snapshot.CSharpTarget.Version);
            Assert.Equal("16.0", snapshot.CSharpTarget.Specified);
            ProjectMessage message = Assert.Single(snapshot.Messages, message => message.Code == ProjectMessage.LanguageVersionUnknown);
            Assert.Contains("14.0", message.Message, StringComparison.Ordinal);
            Assert.Equal(ProjectMessageSeverity.Info, message.Severity);
        }

        [Fact]
        public async Task AMultiTargetedProjectUsesTheSmallestFrameworkVersion()
        {
            ProjectSnapshot snapshot = await LoadFixtureAsync("MultiTarget");

            Assert.Equal(Cs12, snapshot.CSharpTarget.Version);
            Assert.Equal(["net10.0", "net8.0"], snapshot.CSharpTarget.TargetFrameworks);
            Assert.Equal("net10.0", snapshot.TargetFramework);
            Assert.Contains(snapshot.Messages, message => message.Code == ProjectMessage.MultiTargetingUsesFirstFramework);
        }
    }
}
