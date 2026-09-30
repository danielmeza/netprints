using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Catalog.Tests.Sources;

/// <summary>T053: sources resolve through SDK projects; a fake project system and process runner keep the network and the NuGet cache out.</summary>
public sealed class CatalogSourceResolverTests : IDisposable
{
    private const string PackageRoot = "/pkgs";

    private readonly DirectoryInfo directory = Directory.CreateTempSubdirectory("np-sources");

    private readonly List<string> events = [];

    private readonly FakeProcessRunner processes;

    public CatalogSourceResolverTests()
    {
        processes = new FakeProcessRunner(events);
    }

    public void Dispose()
    {
        directory.Delete(recursive: true);
    }

    private string Touch(string relative)
    {
        string path = Path.Combine(directory.FullName, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? directory.FullName);
        File.WriteAllText(path, string.Empty);
        return path;
    }

    private ResolvedCatalogConfig Config(params CatalogSourceConfig[] sources) => new()
    {
        Sources = sources,
        TargetFramework = "net10.0",
        BaseDirectory = directory.FullName,
    };

    private CatalogSourceResolver Resolver(Func<string, ProjectSnapshot> snapshots, out FakeProjectSystem projects)
    {
        projects = new FakeProjectSystem(snapshots, events);
        return new CatalogSourceResolver(projects, processes);
    }

    private static string PackagePath(string id, string version, string file) =>
        Path.Combine(PackageRoot, id.ToLowerInvariant(), version, "lib", "net10.0", file);

    [Fact]
    public async Task AssemblyAndPackageSourcesShareOneTemporaryProjectUnderObj()
    {
        string lib = Touch("libs/MyLib.dll");
        string searchDirectory = Path.Combine(directory.FullName, "extra");
        CatalogSourceResolver resolver = Resolver(path => FakeProjectSystem.Snapshot(path, []), out FakeProjectSystem projects);
        ResolvedCatalogConfig config = Config(new CatalogSourceConfig { Assembly = lib }, new CatalogSourceConfig { Package = "Newtonsoft.Json", Version = "13.0.3" }) with
        {
            ReferencePaths = [searchDirectory],
        };

        CatalogSourceSet set = await resolver.ResolveAsync(config, TestContext.Current.CancellationToken);

        string projectPath = Assert.Single(projects.LoadedPaths);
        string temporary = Assert.Single(set.TemporaryDirectories);
        Assert.Equal(Path.Combine(directory.FullName, "obj", "netprints-catalog"), Path.GetDirectoryName(temporary));
        Assert.Matches("^[0-9a-f]{16}$", Path.GetFileName(temporary));
        Assert.Equal(temporary, Path.GetDirectoryName(projectPath));
        string text = projects.LoadedTexts[0];
        Assert.DoesNotContain("<Project Sdk", text, StringComparison.Ordinal);
        Assert.Contains("<Import Project=\"Sdk.props\" Sdk=\"Microsoft.NET.Sdk\" />", text, StringComparison.Ordinal);
        Assert.Contains("<Import Project=\"Sdk.targets\" Sdk=\"Microsoft.NET.Sdk\" />", text, StringComparison.Ordinal);
        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", text, StringComparison.Ordinal);
        Assert.Contains("<Reference Include=\"MyLib\">", text, StringComparison.Ordinal);
        Assert.Contains("<HintPath>" + lib + "</HintPath>", text, StringComparison.Ordinal);
        Assert.Contains("<PackageReference Include=\"Newtonsoft.Json\" Version=\"13.0.3\" />", text, StringComparison.Ordinal);
        Assert.Contains(searchDirectory, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ThePropertiesThatStopInheritedFilesAreSetBeforeSdkProps()
    {
        CatalogSourceResolver resolver = Resolver(path => FakeProjectSystem.Snapshot(path, []), out FakeProjectSystem projects);

        await resolver.ResolveAsync(Config(new CatalogSourceConfig { Package = "A", Version = "1.0.0" }), TestContext.Current.CancellationToken);

        string text = projects.LoadedTexts[0];
        int sdkProps = text.IndexOf("Sdk=\"Microsoft.NET.Sdk\"", StringComparison.Ordinal);
        foreach (string property in new[] { "ImportDirectoryBuildProps", "ImportDirectoryBuildTargets", "ImportDirectoryPackagesProps", "ManagePackageVersionsCentrally" })
        {
            int index = text.IndexOf($"<{property}>false</{property}>", StringComparison.Ordinal);
            Assert.InRange(index, 0, sdkProps);
        }
    }

    [Fact]
    public async Task ARunUnderCentralPackageManagementAndDirectoryBuildPropsDoesNotInheritThem()
    {
        File.WriteAllText(Path.Combine(directory.FullName, "Directory.Packages.props"), "<Project><PropertyGroup><ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally></PropertyGroup></Project>");
        File.WriteAllText(Path.Combine(directory.FullName, "Directory.Build.props"), "<Project><PropertyGroup><LeakedFromBuildProps>yes</LeakedFromBuildProps></PropertyGroup></Project>");
        CatalogSourceResolver resolver = Resolver(path => FakeProjectSystem.Snapshot(path, []), out FakeProjectSystem projects);
        string searchDirectory = Path.Combine(directory.FullName, "search-here");
        await resolver.ResolveAsync(
            Config(new CatalogSourceConfig { Package = "A", Version = "1.0.0" }) with { ReferencePaths = [searchDirectory] },
            TestContext.Current.CancellationToken);

        ProcessResult result = await new ProcessRunner().RunAsync(
            new ProcessStartRequest(
                "dotnet",
                ["msbuild", projects.LoadedPaths[0], "-getProperty:ManagePackageVersionsCentrally", "-getProperty:LeakedFromBuildProps", "-getProperty:TargetFramework", "-getProperty:AssemblySearchPaths"],
                directory.FullName),
            TestContext.Current.CancellationToken);

        Assert.True(result.ExitCode == 0, result.StandardOutput + result.StandardError);
        Assert.Contains("\"ManagePackageVersionsCentrally\": \"false\"", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"LeakedFromBuildProps\": \"\"", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("\"TargetFramework\": \"net10.0\"", result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains(searchDirectory, result.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("{TargetFrameworkDirectory}", result.StandardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RestoreRunsThroughTheProcessRunnerBeforeTheLoad()
    {
        CatalogSourceResolver resolver = Resolver(path => FakeProjectSystem.Snapshot(path, []), out FakeProjectSystem projects);

        await resolver.ResolveAsync(Config(new CatalogSourceConfig { Package = "A", Version = "1.0.0" }), TestContext.Current.CancellationToken);

        ProcessStartRequest request = Assert.Single(processes.Requests);
        Assert.Equal("dotnet", request.FileName);
        Assert.Equal("restore", request.Arguments[0]);
        Assert.Contains(projects.LoadedPaths[0], request.Arguments);
        Assert.Equal(Path.GetDirectoryName(projects.LoadedPaths[0]), request.WorkingDirectory);
        Assert.Equal(["process", "load"], events);
    }

    [Fact]
    public async Task ARestoreFailureIsAnErrorThatKeepsTheTemporaryProject()
    {
        processes.Result = new ProcessResult(1, string.Empty, "error NU1101: Unable to find package Nope");
        CatalogSourceResolver resolver = Resolver(path => FakeProjectSystem.Snapshot(path, []), out FakeProjectSystem projects);

        CatalogSourceException exception = await Assert.ThrowsAsync<CatalogSourceException>(() =>
            resolver.ResolveAsync(Config(new CatalogSourceConfig { Package = "Nope", Version = "1.0.0" }), TestContext.Current.CancellationToken));

        Assert.Contains("NU1101", exception.Message, StringComparison.Ordinal);
        Assert.Empty(projects.LoadedPaths);
        Assert.NotEmpty(Directory.GetDirectories(Path.Combine(directory.FullName, "obj", "netprints-catalog")));
    }

    [Fact]
    public async Task AnAssemblySourceCatalogsOnlyThatAssemblyAndKeepsTheRestAsReferences()
    {
        string lib = Touch("libs/MyLib.dll");
        string doc = Touch("libs/MyLib.xml");
        ResolvedAssembly runtime = new(Path.Combine(directory.FullName, "ref", "System.Runtime.dll"), null);
        CatalogSourceResolver resolver = Resolver(path => FakeProjectSystem.Snapshot(path, [runtime, new ResolvedAssembly(lib, null)]), out _);

        CatalogSourceSet set = await resolver.ResolveAsync(Config(new CatalogSourceConfig { Assembly = lib }), TestContext.Current.CancellationToken);

        ResolvedAssembly target = Assert.Single(set.Targets);
        Assert.Equal(lib, target.Path);
        Assert.Equal(doc, target.DocumentationPath);
        Assert.Equal(2, set.References.Count);
    }

    [Fact]
    public async Task AnAssemblyPatternExpandsToEveryMatchingFile()
    {
        string first = Touch("libs/A.dll");
        string second = Touch("libs/B.dll");
        Touch("libs/C.txt");
        CatalogSourceResolver resolver = Resolver(path => FakeProjectSystem.Snapshot(path, [new ResolvedAssembly(first, null), new ResolvedAssembly(second, null)]), out FakeProjectSystem projects);

        CatalogSourceSet set = await resolver.ResolveAsync(Config(new CatalogSourceConfig { Assembly = Path.Combine(directory.FullName, "libs", "*.dll") }), TestContext.Current.CancellationToken);

        Assert.Equal([first, second], set.Targets.Select(t => t.Path).Order(StringComparer.Ordinal));
        Assert.Contains("<Reference Include=\"A\">", projects.LoadedTexts[0], StringComparison.Ordinal);
        Assert.Contains("<Reference Include=\"B\">", projects.LoadedTexts[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMissingAssemblyFileIsAnErrorDiagnostic()
    {
        CatalogSourceResolver resolver = Resolver(path => FakeProjectSystem.Snapshot(path, []), out FakeProjectSystem projects);

        CatalogSourceSet set = await resolver.ResolveAsync(
            Config(new CatalogSourceConfig { Assembly = Path.Combine(directory.FullName, "nope.dll") }),
            TestContext.Current.CancellationToken);

        CatalogDiagnostic diagnostic = Assert.Single(set.Diagnostics);
        Assert.Equal(CatalogDiagnosticCodes.UnreferencedAssembly, diagnostic.Code);
        Assert.Equal(CatalogDiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Empty(projects.LoadedPaths);
        Assert.Empty(set.Targets);
    }

    [Fact]
    public async Task PackageAssembliesAreIdentifiedByTheGlobalPackagesPath()
    {
        ResolvedAssembly own = new(PackagePath("Some.Package", "1.2.3", "Some.Package.dll"), PackagePath("Some.Package", "1.2.3", "Some.Package.xml"));
        ResolvedAssembly dependency = new(PackagePath("Dep", "2.0.0", "Dep.dll"), null);
        ResolvedAssembly other = new(PackagePath("Some.Package.Extras", "1.2.3", "Extras.dll"), null);
        Dictionary<string, string> properties = new() { ["NuGetPackageRoot"] = PackageRoot + Path.DirectorySeparatorChar };
        CatalogSourceResolver resolver = Resolver(path => FakeProjectSystem.Snapshot(path, [own, dependency, other], properties), out _);

        CatalogSourceSet set = await resolver.ResolveAsync(Config(new CatalogSourceConfig { Package = "Some.Package", Version = "1.2.3" }), TestContext.Current.CancellationToken);

        Assert.Equal([own], set.Targets);
        Assert.Equal(3, set.References.Count);
    }

    [Fact]
    public async Task APackageWithoutAssembliesIsAnErrorDiagnostic()
    {
        Dictionary<string, string> properties = new() { ["NuGetPackageRoot"] = PackageRoot };
        CatalogSourceResolver resolver = Resolver(path => FakeProjectSystem.Snapshot(path, [], properties), out _);

        CatalogSourceSet set = await resolver.ResolveAsync(Config(new CatalogSourceConfig { Package = "Empty", Version = "1.0.0" }), TestContext.Current.CancellationToken);

        CatalogDiagnostic diagnostic = Assert.Single(set.Diagnostics);
        Assert.Equal(CatalogDiagnosticCodes.UnreferencedAssembly, diagnostic.Code);
        Assert.Contains("Empty", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProjectSourceLoadsTheUserProjectDirectlyAndSelectsReferencesByName()
    {
        string project = Touch("src/App/App.csproj");
        ResolvedAssembly wanted = new(Path.Combine(directory.FullName, "bin", "Wanted.dll"), Path.Combine(directory.FullName, "bin", "Wanted.xml"));
        ResolvedAssembly unwanted = new(Path.Combine(directory.FullName, "bin", "Other.dll"), null);
        CatalogSourceResolver resolver = Resolver(path => FakeProjectSystem.Snapshot(path, [wanted, unwanted]), out FakeProjectSystem projects);

        CatalogSourceSet set = await resolver.ResolveAsync(
            Config(new CatalogSourceConfig { Project = project, Assemblies = ["Wanted", "Missing"] }),
            TestContext.Current.CancellationToken);

        Assert.Equal([project], projects.LoadedPaths);
        Assert.Empty(processes.Requests);
        Assert.Empty(set.TemporaryDirectories);
        Assert.Equal([wanted], set.Targets);
        CatalogDiagnostic diagnostic = Assert.Single(set.Diagnostics);
        Assert.Equal(CatalogDiagnosticCodes.UnreferencedAssembly, diagnostic.Code);
        Assert.Contains("Missing", diagnostic.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheTemporaryDirectoryIsStableForTheSameSourcesAndDeletedOnRequest()
    {
        CatalogSourceResolver resolver = Resolver(path => FakeProjectSystem.Snapshot(path, []), out _);
        ResolvedCatalogConfig config = Config(new CatalogSourceConfig { Package = "A", Version = "1.0.0" });
        ResolvedCatalogConfig changed = Config(new CatalogSourceConfig { Package = "A", Version = "2.0.0" });

        CatalogSourceSet first = await resolver.ResolveAsync(config, TestContext.Current.CancellationToken);
        CatalogSourceSet second = await resolver.ResolveAsync(config, TestContext.Current.CancellationToken);
        CatalogSourceSet third = await resolver.ResolveAsync(changed, TestContext.Current.CancellationToken);

        Assert.Equal(first.TemporaryDirectories, second.TemporaryDirectories);
        Assert.NotEqual(first.TemporaryDirectories, third.TemporaryDirectories);
        Assert.True(Directory.Exists(first.TemporaryDirectories[0]));

        first.DeleteTemporaryDirectories();

        Assert.False(Directory.Exists(first.TemporaryDirectories[0]));
        Assert.True(Directory.Exists(third.TemporaryDirectories[0]));
    }
}
