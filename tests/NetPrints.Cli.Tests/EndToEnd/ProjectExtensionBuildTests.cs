using System;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Cli.Tests.Support;
using NetPrints.Extensibility.Loading;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.EndToEnd;

/// <summary>FR-038: a project-declared extension loads in <c>netprints build</c> with no trust check, as analyzers and build tasks do.</summary>
public sealed class ProjectExtensionBuildTests : IDisposable
{
    private const string Greeting = "Hello from a project extension";

    private readonly SampleCopy _sample = new();

    public void Dispose() => _sample.Dispose();

    private static string TestExtensionFolder() =>
        Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "tests", "NetPrints.TestExtension", "bin", LocalSdkLayout.DetectConfiguration(), "extensions", "netprints.test");

    private async Task DeclareExtensionAsync(string folder)
    {
        string graph = $$"""
            {
              "schemaVersion": 1,
              "namespace": "HelloWorld",
              "name": "Program",
              "visibility": "Public",
              "classGraph": { "nodes": [ { "$kind": "classReturn", "id": "n000000000vny0" } ] },
              "methods": [
                {
                  "id": "m000000001gs20",
                  "name": "Main",
                  "visibility": "Public",
                  "modifiers": "Static",
                  "graph": {
                    "nodes": [
                      { "$kind": "methodEntry", "id": "n000000000vny1" },
                      { "$kind": "return", "id": "n000000000vny2" },
                      { "$kind": "netprints.test/Log", "id": "n000000000vny3" },
                      {
                        "$kind": "callMethod",
                        "id": "n000000000vny4",
                        "pins": [
                          { "pin": "in.data.str0", "value": { "type": "System.String", "value": "{{Greeting}}" } },
                          { "pin": "in.data.str1", "value": { "type": "System.String", "value": "!" } }
                        ],
                        "method": {
                          "name": "Concat",
                          "declaringType": { "name": "System.String" },
                          "parameters": [
                            { "name": "str0", "type": { "name": "System.String" } },
                            { "name": "str1", "type": { "name": "System.String" } }
                          ],
                          "returnTypes": [ { "name": "System.String" } ],
                          "modifiers": "Static",
                          "visibility": "Public"
                        },
                        "pure": true
                      }
                    ],
                    "connections": [
                      { "from": "n000000000vny4/out.data.String", "to": "n000000000vny3/in.data.Value" },
                      { "from": "n000000000vny1/out.exec.Exec", "to": "n000000000vny3/in.exec.Exec" },
                      { "from": "n000000000vny3/out.exec.Then", "to": "n000000000vny2/in.exec.Exec" }
                    ]
                  }
                }
              ]
            }
            """;
        await File.WriteAllTextAsync(_sample.Combine(SampleCopy.GraphName), graph, TestContext.Current.CancellationToken);

        string project = $$"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <RootNamespace>HelloWorld</RootNamespace>
                <NetPrintsProfile>netprints.default</NetPrintsProfile>
              </PropertyGroup>
              <ItemGroup>
                <NetPrintsExtension Include="{{folder}}" />
              </ItemGroup>
            </Project>
            """;
        await File.WriteAllTextAsync(_sample.Project, project, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AProjectDeclaredExtensionTranslatesItsNodesWithNoTrustCheck()
    {
        await DeclareExtensionAsync(TestExtensionFolder());
        File.Delete(_sample.Combine(SampleCopy.GeneratedName));
        var host = new CliTestHost(_sample.Directory);

        int exitCode = await host.RunRealAsync("build", _sample.Project);

        Assert.True(exitCode == ExitCodes.Success, host.Output);
        string generated = await File.ReadAllTextAsync(_sample.Combine(SampleCopy.GeneratedName), TestContext.Current.CancellationToken);
        Assert.Contains("System.Console.WriteLine(", generated, StringComparison.Ordinal);
        Assert.Contains(Greeting, generated, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AProjectDeclaredExtensionThatCannotLoadFailsTheBuildWithItsDiagnostic()
    {
        string broken = Directory.CreateDirectory(_sample.Combine("broken-ext")).FullName;
        await File.WriteAllTextAsync(Path.Combine(broken, "netprints-extension.json"), "{ not json", TestContext.Current.CancellationToken);
        await DeclareExtensionAsync(broken);
        var host = new CliTestHost(_sample.Directory);

        int exitCode = await host.RunRealAsync("build", _sample.Project);

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Contains(ExtensionDiagnosticCodes.InvalidManifest, host.Output, StringComparison.Ordinal);
    }
}
