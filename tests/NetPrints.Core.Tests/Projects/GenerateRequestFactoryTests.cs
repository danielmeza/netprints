using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Generation;
using NetPrints.Projects;
using NetPrints.Testing;
using NetPrints.Tests.Samples;
using NetPrints.Workspace;
using Xunit;

namespace NetPrints.Tests.Projects;

/// <summary>CL-T13 (contracts/cli.md §6): the request built from a project snapshot equals the one the SDK target writes.</summary>
public sealed class GenerateRequestFactoryTests : IDisposable
{
    private readonly string _directory = Directory.CreateTempSubdirectory("netprints-reqfactory-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task FromSnapshotEqualsTheSdkTargetsRspForHelloWorld()
    {
        string sample = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "samples", "HelloWorld");
        foreach (string name in new[] { "HelloWorld.csproj", "HelloWorld.Program.netpc.json" })
        {
            File.Copy(Path.Combine(sample, name), Path.Combine(_directory, name));
        }

        LocalSdkLayout.Write(_directory);
        string project = Path.Combine(_directory, "HelloWorld.csproj");
        (int exit, string output) = await ExternalProcess.RunDotnetAsync(_directory, environment: null, "build", project, "-v:q", "-tl:off", "--nologo");
        Assert.True(exit == 0, output);
        string rsp = Directory.EnumerateFiles(Path.Combine(_directory, "obj"), "netprints.generate.rsp", SearchOption.AllDirectories).First();
        GenerateRequest expected = GenerateRequestFile.Parse(rsp);

        var system = new MsBuildProjectSystem(new ProjectSystemOptions([], "9.9.9-test"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);
        ProjectSnapshot snapshot = await system.LoadAsync(project, TestContext.Current.CancellationToken);
        GenerateRequest actual = GenerateRequestFactory.FromSnapshot(snapshot);

        Assert.Equal(expected.ProjectPath, actual.ProjectPath);
        Assert.Equal(expected.RootNamespace, actual.RootNamespace);
        Assert.Equal(expected.Profile, actual.Profile);
        Assert.Equal(expected.Graphs, actual.Graphs);
        Assert.Equal(expected.Extensions, actual.Extensions);
        Assert.NotEmpty(actual.Graphs);
    }

    [Fact]
    public void FromSnapshotMapsGraphsToTheirGeneratedFilesAndKeepsExtensionFolders()
    {
        string graph = Path.Combine(_directory, "A.netpc.json");
        string folder = Path.Combine(_directory, "ext");
        var snapshot = new ProjectSnapshot(
            Path.Combine(_directory, "P.csproj"), "P", "Root.Ns", "P", BinaryType.Executable, "net10.0", "netprints.default", true,
            [graph], [folder], [], [], [], "{}", new System.Collections.Generic.Dictionary<string, string>(), []);

        GenerateRequest request = GenerateRequestFactory.FromSnapshot(snapshot);

        Assert.Equal(snapshot.ProjectFilePath, request.ProjectPath);
        Assert.Equal("Root.Ns", request.RootNamespace);
        Assert.Equal("netprints.default", request.Profile);
        Assert.Equal(new GraphJob(graph, Path.Combine(_directory, "A.netpc.g.cs")), Assert.Single(request.Graphs));
        Assert.Equal([folder], request.Extensions);
    }

    [Fact]
    public async Task LoadingWithGenerateOnLoadOffLeavesAStaleGeneratedFileAlone()
    {
        string generated = CopyHelloWorldWithStaleGeneratedFile();
        var system = new MsBuildProjectSystem(new ProjectSystemOptions([], "9.9.9-test", GenerateOnLoad: false), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);

        await system.LoadAsync(Path.Combine(_directory, "HelloWorld.csproj"), TestContext.Current.CancellationToken);

        Assert.Equal("// stale\n", await File.ReadAllTextAsync(generated, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoadingWithTheDefaultOptionsRegeneratesAStaleGeneratedFile()
    {
        string generated = CopyHelloWorldWithStaleGeneratedFile();
        var system = new MsBuildProjectSystem(new ProjectSystemOptions([], "9.9.9-test"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);

        await system.LoadAsync(Path.Combine(_directory, "HelloWorld.csproj"), TestContext.Current.CancellationToken);

        Assert.NotEqual("// stale\n", await File.ReadAllTextAsync(generated, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ThePublicLookingSkipPropertyNoLongerSkipsGeneration()
    {
        string generated = CopyHelloWorldWithStaleGeneratedFile();
        string project = Path.Combine(_directory, "HelloWorld.csproj");
        string text = await File.ReadAllTextAsync(project, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(project, text.Replace("<NetPrintsProfile>", "<NetPrintsSkipGenerate>true</NetPrintsSkipGenerate><NetPrintsProfile>", StringComparison.Ordinal), TestContext.Current.CancellationToken);
        var system = new MsBuildProjectSystem(new ProjectSystemOptions([], "9.9.9-test"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance);

        await system.LoadAsync(project, TestContext.Current.CancellationToken);

        Assert.NotEqual("// stale\n", await File.ReadAllTextAsync(generated, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SkippedGenerationLeavesTheFileAndSaysSoInTheBuildLog()
    {
        string generated = CopyHelloWorldWithStaleGeneratedFile();

        (int exit, string output) = await ExternalProcess.RunDotnetAsync(_directory, environment: null,
            "build", Path.Combine(_directory, "HelloWorld.csproj"), "-p:_NetPrintsSkipGenerate=true", "-v:d", "-tl:off", "--nologo");

        Assert.Equal("// stale\n", await File.ReadAllTextAsync(generated, TestContext.Current.CancellationToken));
        Assert.Contains("NetPrintsGenerate skipped", output, StringComparison.Ordinal);
        Assert.True(exit is 0 or 1, output);
    }

    private string CopyHelloWorldWithStaleGeneratedFile()
    {
        string sample = Path.Combine(SampleProjectFactory.FindRepositoryRoot(), "samples", "HelloWorld");
        foreach (string name in new[] { "HelloWorld.csproj", "HelloWorld.Program.netpc.json" })
        {
            File.Copy(Path.Combine(sample, name), Path.Combine(_directory, name));
        }

        LocalSdkLayout.Write(_directory);
        string generated = Path.Combine(_directory, "HelloWorld.Program.netpc.g.cs");
        File.WriteAllText(generated, "// stale\n");
        return generated;
    }
}
