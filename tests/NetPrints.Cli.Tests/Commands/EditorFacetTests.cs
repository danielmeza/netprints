using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Cli.Tests.Support;
using NetPrints.Compilation;
using NetPrints.Extensibility.Loading;
using NetPrints.Generation;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Commands;

/// <summary>US3 scenario 3: hosts without an editor (the generator behind <c>netprints build</c>, the CLI) load an extension that registers editor contributions and ignore them.</summary>
public sealed class EditorFacetTests : IDisposable
{
    private const string ExtensionId = "fx.editor";

    private readonly string _directory = Directory.CreateTempSubdirectory("np-editor-facet-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task TheGeneratorLoadsAnExtensionThatRegistersEditorContributionsWithoutADiagnostic()
    {
        var request = new GenerateRequest(Path.Combine(_directory, "P.csproj"), "T", "netprints.default", [], [FixtureExtensions.EditorFolder()]);

        (var registry, IReadOnlyList<CodeDiagnostic> diagnostics) = GraphCodeGenerator.LoadExtensions(request, TestContext.Current.CancellationToken);

        await using (registry)
        {
            Assert.Empty(diagnostics);
            Assert.Contains(registry.Results.OfType<ExtensionLoadResult.Loaded>(), loaded => loaded.Id == ExtensionId);
            Assert.DoesNotContain(registry.Results, result => result is ExtensionLoadResult.Failed);
        }
    }

    [Fact]
    public async Task TheCatalogCommandLoadsAnExtensionThatRegistersEditorContributions()
    {
        var host = new CliTestHost(_directory);
        string output = Path.Combine(_directory, "out.npcat.json");

        int exitCode = await host.RunRealAsync("catalog", "--assembly", CatalogFixtures.LibraryAssembly, "--extension", FixtureExtensions.EditorFolder(), "--output", output);

        Assert.True(exitCode == ExitCodes.Success, host.Output + host.Error);
        Assert.True(File.Exists(output));
    }
}
