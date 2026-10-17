using System.Collections.Generic;
using System.Linq;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Tests.Projects
{
    /// <summary>
    /// FR-028, FR-070, FR-071, research R19, data-model section 6, US9 scenarios 1 and 2: the language version is parsed by
    /// NetPrints, the keywords resolve against the framework's maximum and <see cref="CSharpLanguageVersion.MaxKnown"/>, and a
    /// multi-targeted project uses the smallest version.
    /// </summary>
    public class CSharpTargetTests
    {
        private static readonly CSharpLanguageVersion Cs12 = new(12, 0);

        private static readonly CSharpLanguageVersion Cs13 = new(13, 0);

        private static CSharpTargetResolution Resolve(params CSharpFrameworkInput[] frameworks) =>
            CSharpTarget.Resolve("P.csproj", frameworks);

        private static CSharpFrameworkInput Net10(string? langVersion) => new("net10.0", langVersion, "14.0");

        private static CSharpFrameworkInput Net8(string? langVersion) => new("net8.0", langVersion, "12.0");

        [Theory]
        [InlineData("12", 12, 0)]
        [InlineData("12.0", 12, 0)]
        [InlineData("7.3", 7, 3)]
        [InlineData(" 13.0 ", 13, 0)]
        public void ANumericVersionParsesWithOrWithoutAMinor(string text, int major, int minor)
        {
            Assert.True(CSharpLanguageVersion.TryParse(text, out CSharpLanguageVersion version));
            Assert.Equal(new CSharpLanguageVersion(major, minor), version);
        }

        [Theory]
        [InlineData("latest")]
        [InlineData("preview")]
        [InlineData("twelve")]
        [InlineData("")]
        [InlineData("12.x")]
        [InlineData("-1")]
        [InlineData(null)]
        public void ANonNumericVersionDoesNotParse(string? text)
        {
            Assert.False(CSharpLanguageVersion.TryParse(text, out _));
        }

        [Theory]
        [InlineData("latest")]
        [InlineData("latestMajor")]
        [InlineData("preview")]
        [InlineData("default")]
        [InlineData("LATEST")]
        public void TheKeywordsAreRecognized(string text)
        {
            Assert.True(CSharpLanguageVersion.IsKeyword(text));
        }

        [Theory]
        [InlineData("12.0")]
        [InlineData("")]
        [InlineData("other")]
        public void OtherTextIsNotAKeyword(string text)
        {
            Assert.False(CSharpLanguageVersion.IsKeyword(text));
        }

        [Fact]
        public void VersionsOrderByMajorThenMinor()
        {
            Assert.True(new CSharpLanguageVersion(7, 3) < new CSharpLanguageVersion(8, 0));
            Assert.True(new CSharpLanguageVersion(12, 1) > Cs12);
            Assert.Equal("12.0", Cs12.ToString());
        }

        [Theory]
        [InlineData("latest")]
        [InlineData("latestMajor")]
        [InlineData("preview")]
        [InlineData("default")]
        public void AKeywordResolvesToTheFrameworkMaximum(string keyword)
        {
            CSharpTargetResolution resolution = Resolve(Net8(keyword));

            Assert.Equal(Cs12, resolution.Target.Version);
            Assert.Equal(keyword, resolution.Target.Specified);
            Assert.Equal(ProjectMessage.LanguageVersionResolved, Assert.Single(resolution.Messages).Code);
        }

        [Fact]
        public void AKeywordIsCappedAtMaxKnown()
        {
            CSharpTargetResolution resolution = Resolve(new CSharpFrameworkInput("net99.0", "latest", "99.0"));

            Assert.Equal(CSharpLanguageVersion.MaxKnown, resolution.Target.Version);
        }

        [Fact]
        public void AMissingMaximumFallsBackToMaxKnown()
        {
            Assert.Equal(CSharpLanguageVersion.MaxKnown, Resolve(new CSharpFrameworkInput("net10.0", "latest", null)).Target.Version);
        }

        [Fact]
        public void NoLangVersionUsesTheFrameworkDefaultWithoutAMessage()
        {
            CSharpTargetResolution resolution = Resolve(Net8(null));

            Assert.Equal(Cs12, resolution.Target.Version);
            Assert.Empty(resolution.Messages);
        }

        [Fact]
        public void AVersionNotNewerThanTheFrameworkIsKept()
        {
            CSharpTargetResolution resolution = Resolve(Net10("10.0"));

            Assert.Equal(new CSharpLanguageVersion(10, 0), resolution.Target.Version);
            Assert.Empty(resolution.Messages);
        }

        [Fact]
        public void AVersionNewerThanTheFrameworkDefaultIsKept()
        {
            CSharpTargetResolution resolution = Resolve(Net8("13.0"));

            Assert.Equal(Cs13, resolution.Target.Version);
            Assert.Equal(ProjectMessage.LanguageVersionNewerThanFramework, Assert.Single(resolution.Messages).Code);
        }

        [Theory]
        [InlineData("16.0")]
        [InlineData("99")]
        [InlineData("nonsense")]
        public void AnUnknownVersionOrOneNewerThanMaxKnownResolvesToMaxKnown(string text)
        {
            CSharpTargetResolution resolution = Resolve(Net10(text));

            Assert.Equal(CSharpLanguageVersion.MaxKnown, resolution.Target.Version);
            Assert.Equal(ProjectMessage.LanguageVersionUnknown, Assert.Single(resolution.Messages).Code);
        }

        [Fact]
        public void AMultiTargetedProjectUsesTheMinimumVersion()
        {
            CSharpTargetResolution resolution = Resolve(Net10("latest"), Net8("latest"));

            Assert.Equal(Cs12, resolution.Target.Version);
            Assert.Equal(["net10.0", "net8.0"], resolution.Target.TargetFrameworks);
        }

        [Fact]
        public void AMultiTargetedProjectReportsEachMessageOnce()
        {
            CSharpTargetResolution resolution = Resolve(Net10("latest"), Net8("latest"));

            IReadOnlyList<ProjectMessage> messages = resolution.Messages;
            Assert.Equal(messages.Count, messages.Select(message => message.Message).Distinct().Count());
            Assert.All(messages, message => Assert.Equal("P.csproj", message.File));
        }

        [Fact]
        public void TheTargetCheckComparesAgainstTheResolvedVersion()
        {
            CSharpTarget target = Resolve(Net8("latest")).Target;

            Assert.True(target.Check(new CSharpLanguageVersion(9, 0)));
            Assert.True(target.Check(Cs12));
            Assert.False(target.Check(Cs13));
        }

        [Fact]
        public void TheUnconstrainedTargetIsMaxKnownWithNoFrameworks()
        {
            CSharpTarget target = CSharpTarget.Unconstrained;

            Assert.Equal(CSharpLanguageVersion.MaxKnown, target.Version);
            Assert.Empty(target.TargetFrameworks);
            Assert.True(target.Check(CSharpLanguageVersion.MaxKnown));
        }

        [Fact]
        public void AProjectSnapshotDefaultsToTheUnconstrainedTargetAndProjectExposesIt()
        {
            var snapshot = new ProjectSnapshot("P.csproj", "P", "P", "P", NetPrints.Core.BinaryType.SharedLibrary, "net10.0", "", false,
                [], [], [], [], [], "{}", new Dictionary<string, string>(), []);
            Assert.Same(CSharpTarget.Unconstrained, snapshot.CSharpTarget);

            CSharpTarget target = Resolve(Net8("latest")).Target;
            var project = NetPrints.Core.Project.FromSnapshot(snapshot with { CSharpTarget = target });
            Assert.Same(target, project.CSharpTarget);
        }
    }
}
