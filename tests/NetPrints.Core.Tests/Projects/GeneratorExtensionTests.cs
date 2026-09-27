using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Generator;
using NetPrints.Tests.Extensibility;
using NetPrints.Tests.Samples;
using Xunit;

namespace NetPrints.Tests.Projects;

/// <summary>PS-T14 (project-system.md §7): the generator loads the request's <c>extension=</c> folders (§3).</summary>
public class GeneratorExtensionTests : IDisposable
{
    private readonly string directory = Directory.CreateTempSubdirectory("netprints-generator-ext-").FullName;

    public void Dispose() => Directory.Delete(directory, recursive: true);

    private static string GeneratorDll => Path.Combine(
        SampleProjectFactory.FindRepositoryRoot(), "src", "NetPrints.Generator", "bin", LocalSdkLayout.DetectConfiguration(), "net10.0", "NetPrints.Generator.dll");

    private async Task<string> WriteLogGraphAsync(string name = "Logs")
    {
        await using ExtensionRegistry registry = ExtensionTestSupport.Load(new ExtensionLoaderOptions([], [TestExtensionLocation.Folder], [BuiltInExtension.InProcessEntry]));
        (ClassGraph cls, _) = ExtensionGraphs.BuildLogClass(registry, "PsT14", name);
        return await ExtensionGraphs.WriteAsync(registry, cls, Path.Combine(directory, $"{name}.netpc.json"));
    }

    private static string WriteProject(string directory, string name, string extensionItems)
    {
        string path = Path.Combine(directory, $"{name}.csproj");
        File.WriteAllText(path, $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>PsT14</RootNamespace>
                <NetPrintsProfile>netprints.default</NetPrintsProfile>
              </PropertyGroup>
              <ItemGroup>
                {extensionItems}
              </ItemGroup>
            </Project>

            """);
        return path;
    }

    private async Task<(int ExitCode, string Output)> RunGeneratorAsync(string graphPath, params string[] extensionFolders)
    {
        string request = Path.Combine(directory, "netprints.generate.rsp");
        var lines = new List<string>
        {
            $"project={Path.Combine(directory, "PsT14.csproj")}",
            "rootNamespace=PsT14",
            "profile=netprints.default",
        };
        lines.AddRange(Array.ConvertAll(extensionFolders, folder => $"extension={folder}"));
        lines.Add($"graph={graphPath}|{Path.ChangeExtension(graphPath, ".g.cs")}");
        await File.WriteAllLinesAsync(request, lines, TestContext.Current.CancellationToken);
        return await ExternalProcess.RunDotnetAsync(directory, environment: null, "exec", GeneratorDll, "generate", request);
    }

    [Fact]
    public async Task ABuildWithTheExtensionItemGeneratesAndCompilesTheExtensionNode()
    {
        LocalSdkLayout.Write(directory);
        await WriteLogGraphAsync();
        string extensionFolder = TestExtensionLocation.CopyTo(Path.Combine(directory, "ext"));
        string csproj = WriteProject(directory, "PsT14", $"""<NetPrintsExtension Include="{extensionFolder}" />""");

        (int exit, string output) = await ExternalProcess.RunDotnetAsync(directory, environment: null, "build", csproj, "-v:n", "-tl:off", "--nologo");

        Assert.True(exit == 0, output);
        string generated = await File.ReadAllTextAsync(Path.Combine(directory, "Logs.netpc.g.cs"), TestContext.Current.CancellationToken);
        Assert.Contains("varValue = \"hello\";", generated, StringComparison.Ordinal);
        Assert.Contains("System.Console.WriteLine(varValue);", generated, StringComparison.Ordinal);
        Assert.Contains("[System.Obsolete(\"test\")]", generated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABuildWithoutTheExtensionItemFailsWithNpt003AndWritesNothing()
    {
        LocalSdkLayout.Write(directory);
        await WriteLogGraphAsync();
        string csproj = WriteProject(directory, "PsT14", string.Empty);

        (int exit, string output) = await ExternalProcess.RunDotnetAsync(directory, environment: null, "build", csproj, "-v:n", "-tl:off", "--nologo");

        Assert.NotEqual(0, exit);
        Assert.Contains("Logs.netpc.json", output, StringComparison.Ordinal);
        Assert.Contains("error NPT003:", output, StringComparison.Ordinal);
        Assert.Contains("netprints.test/Log", output, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Combine(directory, "Logs.netpc.g.cs")));
    }

    [Fact]
    public async Task AMissingGraphExtensionKeepsThePreviousGeneratedFile()
    {
        string graphPath = await WriteLogGraphAsync();
        string generatedPath = Path.ChangeExtension(graphPath, ".g.cs");
        await File.WriteAllTextAsync(generatedPath, "// previous\n", TestContext.Current.CancellationToken);

        (int exit, string output) = await RunGeneratorAsync(graphPath);

        Assert.Equal(1, exit);
        Assert.Contains("error NPT003:", output, StringComparison.Ordinal);
        Assert.Equal("// previous\n", await File.ReadAllTextAsync(generatedPath, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheGeneratorLoadsTheRequestsExtensionFolderAndExitsZero()
    {
        string graphPath = await WriteLogGraphAsync();
        string extensionFolder = TestExtensionLocation.CopyTo(Path.Combine(directory, "ext"));

        (int exit, string output) = await RunGeneratorAsync(graphPath, extensionFolder);

        Assert.True(exit == 0, output);
        string generated = await File.ReadAllTextAsync(Path.ChangeExtension(graphPath, ".g.cs"), TestContext.Current.CancellationToken);
        Assert.Contains("varValue = \"hello\";", generated, StringComparison.Ordinal);
        Assert.Contains("System.Console.WriteLine(varValue);", generated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("missing-assembly", "NPX007")]
    [InlineData("api-2", "NPX002")]
    [InlineData("no-manifest", "NPX001")]
    public async Task AnExtensionThatFailsToLoadIsAnErrorNamingItAndTheGeneratorExitsOne(string kind, string code)
    {
        string graphPath = await WriteLogGraphAsync();
        string folder = Path.Combine(directory, "broken");
        Directory.CreateDirectory(folder);
        switch (kind)
        {
            case "missing-assembly":
                ExtensionTestSupport.WriteManifest(folder, ExtensionTestSupport.ManifestJson("test.broken"));
                break;
            case "api-2":
                ExtensionTestSupport.WriteManifest(folder, ExtensionTestSupport.ManifestJson("test.broken", api: "2.0"));
                break;
        }

        (int exit, string output) = await RunGeneratorAsync(graphPath, folder);

        Assert.Equal(1, exit);
        Assert.Contains($"error {code}:", output, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.ChangeExtension(graphPath, ".g.cs")));
    }

    [Fact]
    public async Task LoadExtensionsLoadsTheBuiltInExtensionThenTheRequestsFoldersAndNothingElse()
    {
        string folder = TestExtensionLocation.CopyTo(Path.Combine(directory, "ext"));
        var request = new GenerateRequest("p.csproj", "P", "netprints.default", [], [folder]);

        (ExtensionRegistry registry, IReadOnlyList<CodeDiagnostic> diagnostics) = GraphCodeGenerator.LoadExtensions(request, TestContext.Current.CancellationToken);
        await using (registry)
        {
            Assert.Empty(diagnostics);
            Assert.Equal(["netprints", "netprints.test"], registry.Loaded.Select(m => m.Id));
        }
    }
}
