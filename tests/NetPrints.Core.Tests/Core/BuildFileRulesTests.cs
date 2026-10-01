using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace NetPrints.Tests.Core
{
    /// <summary>The build-file hygiene rules catch each ordinary spelling of the entry they guard (ADR-0003, ADR-0017).</summary>
    public class BuildFileRulesTests
    {
        private static XDocument Project(string body) => XDocument.Parse($"<Project Sdk=\"Microsoft.NET.Sdk\">{body}</Project>");

        [Theory]
        [InlineData("<ItemGroup><NetPrintsExperimentalOptIn Condition=\"true\" Include=\"CS8602\" /></ItemGroup>")]
        [InlineData("<ItemGroup><NetPrintsExperimentalOptIn Include='CS8602' /></ItemGroup>")]
        [InlineData("<ItemGroup><NetPrintsExperimentalOptIn Include=\"NPXE0001;CS8602\" /></ItemGroup>")]
        [InlineData("<ItemGroup><NetPrintsExperimentalOptIn Update=\"CS8602\" /></ItemGroup>")]
        public void OptInIdsReadEveryAttributeOrderQuoteStyleAndCondition(string body)
        {
            Assert.Contains("CS8602", BuildFileRules.OptInIds(Project(body)));
        }

        [Theory]
        [InlineData("<PropertyGroup><NoWarn>CS1591</NoWarn></PropertyGroup>")]
        [InlineData("<PropertyGroup><NoWarn Condition=\"'$(Configuration)'=='Debug'\">CS1591</NoWarn></PropertyGroup>")]
        [InlineData("<PropertyGroup><NoWarn >CS1591</NoWarn></PropertyGroup>")]
        [InlineData("<PropertyGroup><WarningsNotAsErrors Condition='true'>CS1591</WarningsNotAsErrors></PropertyGroup>")]
        [InlineData("<PropertyGroup><TreatWarningsAsErrors Condition=\"true\"> False </TreatWarningsAsErrors></PropertyGroup>")]
        [InlineData("<PropertyGroup><MSBuildWarningsAsMessages Condition=\"true\">NU1701</MSBuildWarningsAsMessages></PropertyGroup>")]
        [InlineData("<ItemGroup><GlobalAnalyzerConfigFiles Include=\"cfg/analysis-rules.txt\" /></ItemGroup>")]
        [InlineData("<ItemGroup><EditorConfigFiles Include='cfg/x.txt' /></ItemGroup>")]
        [InlineData("<ItemGroup><Analyzer Remove=\"@(Analyzer)\" Condition=\"'%(Analyzer.NuGetPackageId)' == 'SonarAnalyzer.CSharp'\" /></ItemGroup>")]
        [InlineData("<Target Name=\"T\"><ItemGroup><Analyzer Condition=\"true\" Remove='@(Analyzer)' /></ItemGroup></Target>")]
        public void WarningOffendersFindEveryVariantOfAWarningBarChange(string body)
        {
            Assert.NotEmpty(BuildFileRules.WarningOffenders("src/X/X.csproj", Project(body)));
        }

        [Fact]
        public void TheOptInNoWarnIsAllowedOnlyAsTheBareLineInTheRootTargetsFile()
        {
            XDocument optIn = Project($"<Target Name=\"NetPrintsExperimentalOptIn\"><PropertyGroup><NoWarn>{BuildFileRules.OptInNoWarnValue}</NoWarn></PropertyGroup></Target>");
            XDocument conditioned = Project($"<Target Name=\"NetPrintsExperimentalOptIn\"><PropertyGroup><NoWarn Condition=\"true\">{BuildFileRules.OptInNoWarnValue}</NoWarn></PropertyGroup></Target>");
            XDocument twice = Project($"<PropertyGroup><NoWarn>{BuildFileRules.OptInNoWarnValue}</NoWarn><NoWarn>CS1591</NoWarn></PropertyGroup>");

            Assert.Empty(BuildFileRules.WarningOffenders(BuildFileRules.OptInTargetsFile, optIn));
            Assert.NotEmpty(BuildFileRules.WarningOffenders("src/X/Directory.Build.targets", optIn));
            Assert.NotEmpty(BuildFileRules.WarningOffenders(BuildFileRules.OptInTargetsFile, conditioned));
            Assert.Equal(2, BuildFileRules.WarningOffenders(BuildFileRules.OptInTargetsFile, twice).Count);
        }

        [Fact]
        public void TheListedEngineSuppressionAndAnOrdinaryPropertyAreNotOffenders()
        {
            XDocument props = Project("<PropertyGroup><MSBuildWarningsAsMessages>$(MSBuildWarningsAsMessages);MINVER1001</MSBuildWarningsAsMessages><TreatWarningsAsErrors>true</TreatWarningsAsErrors></PropertyGroup><ItemGroup><Analyzer Include=\"x.dll\" /></ItemGroup>");

            Assert.Empty(BuildFileRules.WarningOffenders("Directory.Build.props", props));
            Assert.Single(BuildFileRules.WarningOffenders("src/Directory.Build.props", props));
        }

        [Fact]
        public void TheNpxe0004DuplicateIsAllowedOnlyAsOneDeclarationInEachOfTheTwoNamedFiles()
        {
            string[] both = ["NetPrints.Core/ExperimentalApiIds.cs", "NetPrints.Catalog/Engine/ExperimentalApis.cs"];

            Assert.True(SourceHygieneTests.IsAllowedDuplicate("NPXE0004", both));
            Assert.False(SourceHygieneTests.IsAllowedDuplicate("NPXE0003", both));
            Assert.False(SourceHygieneTests.IsAllowedDuplicate("NPXE0004", [.. both, "NetPrints.Catalog/Engine/CatalogDiagnosticCodes.cs"]));
            Assert.False(SourceHygieneTests.IsAllowedDuplicate("NPXE0004", [.. both, both[1]]));
            Assert.False(SourceHygieneTests.IsAllowedDuplicate("NPXE0004", [both[0], both[0]]));
            Assert.False(SourceHygieneTests.IsAllowedDuplicate("NPXE0004", [both[0], "NetPrints.Catalog/Engine/CatalogDiagnosticCodes.cs"]));
        }
    }
}
