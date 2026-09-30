using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Cli.Tests.Support;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Commands;

/// <summary>CT-T11, CT-T12, CT-T13: <c>netprints catalog</c> against the real SDK, the fixture library and the fixture extension.</summary>
public sealed class CatalogCommandTests : IDisposable, IClassFixture<PackedFixtureFeed>
{
    private const string PublicApiSnapshot = "public-api.npcat.json";
    private const string FlagsSnapshot = "fixture-flags.npcat.json";
    private const string FixtureId = "catalogfixturelib";
    private const string FlagsId = "catalogfixturelib-flags";
    private const string OutputName = "out.npcat.json";

    private readonly string _directory = Directory.CreateTempSubdirectory("np-catalog-").FullName;
    private readonly PackedFixtureFeed _feed;
    private readonly CliTestHost _host;

    public CatalogCommandTests(PackedFixtureFeed feed)
    {
        _feed = feed;
        _host = new CliTestHost(_directory);
    }

    private string Output => Path.Combine(_directory, OutputName);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task AnAssemblySourceWritesTheFixtureCatalogAndASecondRunWritesNothing()
    {
        int first = await _host.RunRealAsync("catalog", "--assembly", CatalogFixtures.LibraryAssembly, "--output", Output);

        Assert.True(first == ExitCodes.Success, _host.Output + _host.Error);
        Assert.Equal(CatalogFixtures.Snapshot(PublicApiSnapshot), File.ReadAllText(Output));
        Assert.Contains("wrote " + Output, _host.Output, StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(_directory, "obj")));

        DateTime modified = File.GetLastWriteTimeUtc(Output);
        var second = new CliTestHost(_directory);

        Assert.Equal(ExitCodes.Success, await second.RunRealAsync("catalog", "--assembly", CatalogFixtures.LibraryAssembly, "--output", Output));

        Assert.Equal(modified, File.GetLastWriteTimeUtc(Output));
        Assert.Contains("up to date: " + Output, second.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("wrote ", second.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckPassesForAnUpToDateFileAndFailsAfterTheExcludeChanges()
    {
        string[] source = ["--assembly", CatalogFixtures.LibraryAssembly, "--output", Output];
        Assert.Equal(ExitCodes.Success, await _host.RunRealAsync(["catalog", .. source]));
        DateTime modified = File.GetLastWriteTimeUtc(Output);

        var same = new CliTestHost(_directory);
        Assert.Equal(ExitCodes.Success, await same.RunRealAsync(["catalog", .. source, "--check"]));
        Assert.DoesNotContain("stale: ", same.Output, StringComparison.Ordinal);

        var changed = new CliTestHost(_directory);
        int exitCode = await changed.RunRealAsync(["catalog", .. source, "--check", "--exclude", "Fixture.Geometry.*"]);

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Contains("stale: " + Output, changed.Output, StringComparison.Ordinal);
        Assert.Equal(modified, File.GetLastWriteTimeUtc(Output));
        Assert.Equal(CatalogFixtures.Snapshot(PublicApiSnapshot), File.ReadAllText(Output));
    }

    [Fact]
    public async Task CheckTreatsAMissingFileAsStaleWithoutCreatingIt()
    {
        int exitCode = await _host.RunRealAsync("catalog", "--assembly", CatalogFixtures.LibraryAssembly, "--output", Output, "--check");

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Contains("stale: " + Output, _host.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(Output));
    }

    [Fact]
    public async Task TheConfigurationFileInTheCurrentDirectoryIsUsedAndItsRelativePathsResolveAgainstIt()
    {
        string library = Path.Combine(_directory, "lib");
        Directory.CreateDirectory(library);
        foreach (string file in Directory.EnumerateFiles(CatalogFixtures.LibraryOutput, CatalogFixtures.LibraryName + ".*").Where(f => !f.EndsWith(".pdb", StringComparison.Ordinal)))
        {
            File.Copy(file, Path.Combine(library, Path.GetFileName(file)));
        }

        File.WriteAllText(
            Path.Combine(_directory, "netprints.catalog.json"),
            """{ "schemaVersion": 1, "sources": [ { "assembly": "lib/CatalogFixtureLib.dll" } ], "output": { "path": "catalogs/fixture.npcat.json" } }""");

        int exitCode = await _host.RunRealAsync("catalog");

        Assert.True(exitCode == ExitCodes.Success, _host.Output + _host.Error);
        Assert.Equal(CatalogFixtures.Snapshot(PublicApiSnapshot), File.ReadAllText(Path.Combine(_directory, "catalogs", "fixture.npcat.json")));
    }

    [Fact]
    public async Task TheCSharpFormatWritesTheClassNextToTheDirectory()
    {
        int exitCode = await _host.RunRealAsync(
            "catalog", "--assembly", CatalogFixtures.LibraryAssembly, "--format", "csharp", "--class-name", "FixtureCatalog", "--namespace", "My.Catalogs");

        string file = Path.Combine(_directory, "FixtureCatalog.g.cs");
        Assert.True(exitCode == ExitCodes.Success, _host.Output + _host.Error);
        string text = File.ReadAllText(file);
        Assert.StartsWith("// <auto-generated/> netprints catalog", text, StringComparison.Ordinal);
        Assert.Contains("namespace My.Catalogs;", text, StringComparison.Ordinal);
        Assert.Contains("internal static partial class FixtureCatalog", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProfileFileSelectsTheCatalogFixtureFlagsProducesTheSnapshot()
    {
        string profile = Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "tests", "NetPrints.Catalog.Tests", "Profiles", "fixture-flags.npprofile.json");

        int exitCode = await _host.RunRealAsync("catalog", "--assembly", CatalogFixtures.LibraryAssembly, "--profile", profile, "--id", FlagsId, "--output", Output);

        Assert.True(exitCode == ExitCodes.Success, _host.Output + _host.Error);
        Assert.Equal(CatalogFixtures.Snapshot(FlagsSnapshot), File.ReadAllText(Output));
    }

    [Fact]
    public async Task AProfileContributedByAnExtensionFolderIsResolvableById()
    {
        int exitCode = await _host.RunRealAsync(
            "catalog", "--assembly", CatalogFixtures.LibraryAssembly, "--extension", FixtureExtensions.CatalogFolder(), "--profile", "fixture-flags", "--id", FlagsId, "--output", Output);

        Assert.True(exitCode == ExitCodes.Success, _host.Output + _host.Error);
        Assert.Equal(CatalogFixtures.Snapshot(FlagsSnapshot), File.ReadAllText(Output));
    }

    [Fact]
    public async Task APackageFromALocalFeedProducesTheSameCatalogAsTheAssembly()
    {
        _feed.WriteNuGetConfig(_directory);

        int exitCode = await _host.RunRealAsync("catalog", "--package", $"{CatalogFixtures.LibraryName}@{PackedFixtureFeed.PackageVersion}", "--output", Output);

        Assert.True(exitCode == ExitCodes.Success, _host.Output + _host.Error);
        Assert.Equal(CatalogFixtures.Snapshot(PublicApiSnapshot), File.ReadAllText(Output));
    }

    [Fact]
    public async Task AnUnknownPackageExitsOneAndKeepsTheRestoreDirectory()
    {
        _feed.WriteNuGetConfig(_directory);

        int exitCode = await _host.RunRealAsync("catalog", "--package", "No.Such.Package@9.9.9", "--output", Output);

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Contains("restore failed", _host.Error.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Output));
        Assert.NotEmpty(Directory.EnumerateDirectories(Path.Combine(_directory, "obj", "netprints-catalog")));
    }

    [Fact]
    public async Task AnAssemblyThatMatchesNothingReportsNpc001AndExitsOne()
    {
        int exitCode = await _host.RunRealAsync("catalog", "--assembly", Path.Combine(_directory, "Missing.dll"), "--output", Output);

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Contains("NPC001", _host.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(Output));
    }

    [Fact]
    public async Task TheProjectProfilesCatalogProfileIsUsedWithoutTheProfileOption()
    {
        string project = WriteNetPrintsProject();

        int exitCode = await _host.RunRealAsync("catalog", "--project", project, "--assemblies", CatalogFixtures.LibraryName, "--id", FlagsId, "--output", Output);

        Assert.True(exitCode == ExitCodes.Success, _host.Output + _host.Error);
        Assert.Equal(CatalogFixtures.Snapshot(FlagsSnapshot), File.ReadAllText(Output));
    }

    [Fact]
    public async Task AnExplicitProfileWinsOverTheProjectProfile()
    {
        string project = WriteNetPrintsProject();

        int exitCode = await _host.RunRealAsync("catalog", "--project", project, "--assemblies", CatalogFixtures.LibraryName, "--profile", "public-api", "--output", Output);

        Assert.True(exitCode == ExitCodes.Success, _host.Output + _host.Error);
        Assert.Equal(CatalogFixtures.Snapshot(PublicApiSnapshot), File.ReadAllText(Output));
        Assert.Contains(FixtureId, File.ReadAllText(Output), StringComparison.Ordinal);
    }

    [Fact]
    public async Task NeitherAConfigurationFileNorASourceIsUsage()
    {
        int exitCode = await _host.RunAsync("catalog");

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("netprints.catalog.json", _host.Error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--format", "xml")]
    [InlineData("--package", "NoVersion")]
    public async Task InvalidOptionValuesAreUsage(string option, string value)
    {
        int exitCode = await _host.RunAsync("catalog", "--assembly", CatalogFixtures.LibraryAssembly, option, value);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.NotEmpty(_host.Error.ToString());
    }

    [Fact]
    public async Task AnUnknownProfileOptionIsUsageAndPrintsNpc002()
    {
        int exitCode = await _host.RunAsync("catalog", "--assembly", CatalogFixtures.LibraryAssembly, "--profile", "no-such-profile");

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("catalog: error NPC002: Unknown catalog profile 'no-such-profile'. Available: public-api", _host.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownProfileInTheConfigurationFileExitsOneAndPrintsNpc002()
    {
        string assembly = CatalogFixtures.LibraryAssembly.Replace('\\', '/');
        File.WriteAllText(
            Path.Combine(_directory, "netprints.catalog.json"),
            $$"""{ "schemaVersion": 1, "sources": [ { "assembly": "{{assembly}}" } ], "profile": "no-such-profile" }""");

        int exitCode = await _host.RunAsync("catalog");

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Contains("catalog: error NPC002: Unknown catalog profile 'no-such-profile'", _host.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnInvalidProfileFileExitsOneAndPrintsNpc003()
    {
        string profile = Path.Combine(_directory, "bad.npprofile.json");
        File.WriteAllText(profile, "{ not json");

        int exitCode = await _host.RunAsync("catalog", "--assembly", CatalogFixtures.LibraryAssembly, "--profile", profile);

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Contains("catalog: error NPC003:", _host.Error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--class-name", "1Bad")]
    [InlineData("--class-name", "class")]
    [InlineData("--namespace", "My..Catalogs")]
    [InlineData("--namespace", "My.namespace")]
    public async Task AnInvalidClassNameOrNamespaceIsUsageBeforeAnySdkIsNeeded(string option, string value)
    {
        _host.MsBuild.Available = false;

        int exitCode = await _host.RunAsync("catalog", "--assembly", CatalogFixtures.LibraryAssembly, "--format", "csharp", option, value);

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains(value, _host.Error.ToString(), StringComparison.Ordinal);
        Assert.Equal(0, _host.MsBuild.Calls);
    }

    [Theory]
    [InlineData("_Private", "private")]
    [InlineData("My Lib", "my-lib")]
    public async Task TheIdAndTheOutputFileAreDerivedFromTheAssemblyName(string assemblyName, string expectedId)
    {
        string assembly = Path.Combine(_directory, assemblyName + ".dll");
        CSharpCompilation compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText("public class Marker { }", cancellationToken: TestContext.Current.CancellationToken)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        Assert.True(compilation.Emit(assembly, cancellationToken: TestContext.Current.CancellationToken).Success);

        int exitCode = await _host.RunRealAsync("catalog", "--assembly", assembly);

        Assert.True(exitCode == ExitCodes.Success, _host.Output + _host.Error);
        string written = Path.Combine(_directory, expectedId + ".npcat.json");
        Assert.True(File.Exists(written), _host.Output);
        Assert.Contains($"\"id\": \"{expectedId}\"", File.ReadAllText(written), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMissingConfigurationFileNamedWithConfigIsUsage()
    {
        int exitCode = await _host.RunAsync("catalog", "--config", Path.Combine(_directory, "missing.json"));

        Assert.Equal(ExitCodes.Usage, exitCode);
        Assert.Contains("missing.json", _host.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheProjectIsLoadedOncePerRun()
    {
        string project = Path.Combine(_directory, "App.csproj");
        File.WriteAllText(project, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        int exitCode = await _host.RunAsync("catalog", "--project", project, "--assemblies", "Anything");

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Contains("NPC001", _host.Output, StringComparison.Ordinal);
        Assert.Equal([project], _host.Projects.LoadedProjects);
    }

    [Fact]
    public async Task ADependencyInAnotherDirectoryIsFoundThroughTheReferencePath()
    {
        (string dependent, string dependencies) = SplitDependentFromItsDependency();

        int without = await _host.RunRealAsync("catalog", "--assembly", dependent, "--output", Output);
        string missing = File.ReadAllText(Output);
        var withPath = new CliTestHost(_directory);
        int with = await withPath.RunRealAsync("catalog", "--assembly", dependent, "--reference-path", dependencies, "--output", Output);

        Assert.Equal(ExitCodes.Success, without);
        Assert.Contains("NPC005", _host.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("\"Take\"", missing, StringComparison.Ordinal);
        Assert.True(with == ExitCodes.Success, withPath.Output + withPath.Error);
        Assert.DoesNotContain("NPC005", withPath.Output, StringComparison.Ordinal);
        Assert.Contains("\"Take\"", File.ReadAllText(Output), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ADependencyNextToTheAssemblyIsFoundWithoutAReferencePath()
    {
        (string dependent, string dependencies) = SplitDependentFromItsDependency();
        File.Copy(Path.Combine(dependencies, CatalogFixtures.LibraryName + ".dll"), Path.Combine(Path.GetDirectoryName(dependent) ?? _directory, CatalogFixtures.LibraryName + ".dll"));

        int exitCode = await _host.RunRealAsync("catalog", "--assembly", dependent, "--output", Output);

        Assert.True(exitCode == ExitCodes.Success, _host.Output + _host.Error);
        Assert.DoesNotContain("NPC005", _host.Output, StringComparison.Ordinal);
        Assert.Contains("\"Take\"", File.ReadAllText(Output), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnInvalidConfigurationFileExitsOne()
    {
        File.WriteAllText(Path.Combine(_directory, "netprints.catalog.json"), "{ not json");

        int exitCode = await _host.RunAsync("catalog");

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.NotEmpty(_host.Error.ToString());
    }

    [Fact]
    public async Task ANewerConfigurationSchemaExitsOneNamingBothVersions()
    {
        File.WriteAllText(Path.Combine(_directory, "netprints.catalog.json"), """{ "schemaVersion": 2, "sources": [ { "assembly": "a.dll" } ] }""");

        int exitCode = await _host.RunAsync("catalog");

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Contains("2", _host.Error.ToString(), StringComparison.Ordinal);
    }

    private (string Dependent, string Dependencies) SplitDependentFromItsDependency()
    {
        string dependentDirectory = Path.Combine(_directory, "dependent");
        string dependencies = Path.Combine(_directory, "dependencies");
        Directory.CreateDirectory(dependentDirectory);
        Directory.CreateDirectory(dependencies);
        string dependent = Path.Combine(dependentDirectory, CatalogFixtures.DependentName + ".dll");
        File.Copy(CatalogFixtures.DependentAssembly, dependent);
        File.Copy(CatalogFixtures.LibraryAssembly, Path.Combine(dependencies, CatalogFixtures.LibraryName + ".dll"));
        return (dependent, dependencies);
    }

    private string WriteNetPrintsProject()
    {
        string directory = Path.Combine(_directory, "project");
        LocalSdkLayout.Write(directory);
        string path = Path.Combine(directory, "Fx.csproj");
        File.WriteAllText(path, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <NetPrintsProfile>fx.catalog.profile</NetPrintsProfile>
              </PropertyGroup>
              <ItemGroup>
                <Reference Include="{CatalogFixtures.LibraryName}">
                  <HintPath>{CatalogFixtures.LibraryAssembly}</HintPath>
                </Reference>
                <NetPrintsExtension Include="{FixtureExtensions.CatalogFolder()}" />
              </ItemGroup>
            </Project>
            """);
        return path;
    }
}
