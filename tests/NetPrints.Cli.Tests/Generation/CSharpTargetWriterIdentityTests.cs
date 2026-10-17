using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Cli.Tests.Support;
using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Generation;
using NetPrints.Projects;
using NetPrints.Serialization;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Json;
using NetPrints.Serialization.Mapping;
using NetPrints.Serialization.Migrations;
using NetPrints.Testing;
using NetPrints.Translator;
using NetPrints.Workspace;
using Xunit;

namespace NetPrints.Cli.Tests.Generation;

/// <summary>
/// FR-072, SC-010, ADR-0038 decision 1: for each of the six C# target fixtures the editor's save path, <c>netprints regen</c> and
/// the SDK's generate target write byte-identical files, and the request the SDK writes carries the target the project system resolved.
/// </summary>
public sealed class CSharpTargetWriterIdentityTests
{
    private const string GraphName = SampleCopy.GraphName;

    private const string GeneratedName = SampleCopy.GeneratedName;

    private const string ShortEscape = "\"Hello,\\eWorld!\"";

    private const string LongEscape = "\"Hello,\\u001bWorld!\"";

    [Theory]
    [InlineData("NoLangVersion", ShortEscape)]
    [InlineData("Latest", ShortEscape)]
    [InlineData("Preview", ShortEscape)]
    [InlineData("Pinned10", LongEscape)]
    [InlineData("Unknown16", ShortEscape)]
    [InlineData("MultiTarget", LongEscape)]
    public async Task TheEditorRegenAndTheSdkWriteTheSameBytes(string fixture, string expectedLiteral)
    {
        MsBuildRegistration.EnsureRegistered(NullLogger.Instance);
        string directory = PrepareFixture(fixture);
        try
        {
            string project = Path.Combine(directory, $"{fixture}.csproj");
            string generated = Path.Combine(directory, GeneratedName);
            var host = new CliTestHost(directory);

            ProjectSnapshot snapshot = await LoadSnapshotAsync(project);
            string editor = await RenderLikeTheEditorAsync(snapshot, Path.Combine(directory, GraphName));

            Assert.Equal(0, await host.RunRealAsync("regen", project));
            string regen = await File.ReadAllTextAsync(generated, TestContext.Current.CancellationToken);
            File.Delete(generated);

            await host.RunRealAsync("build", project);
            string sdk = await File.ReadAllTextAsync(generated, TestContext.Current.CancellationToken);

            Assert.Equal(editor, regen);
            Assert.Equal(regen, sdk);
            Assert.Contains(expectedLiteral, editor, StringComparison.Ordinal);

            string[] requests = Directory.GetFiles(Path.Combine(directory, "obj"), "netprints.generate.rsp", SearchOption.AllDirectories);
            Assert.NotEmpty(requests);
            foreach (string requestPath in requests)
            {
                CSharpTarget target = GenerateRequestFile.Parse(requestPath).Target;
                Assert.Equal(snapshot.CSharpTarget.Version, target.Version);
                Assert.Equal(snapshot.CSharpTarget.Specified, target.Specified);
                Assert.Equal(snapshot.CSharpTarget.TargetFrameworks, target.TargetFrameworks);
            }
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ARequestWithoutFrameworkLinesIsUnconstrained()
    {
        string path = Path.Combine(Path.GetTempPath(), $"netprints-rsp-{Guid.NewGuid():N}.rsp");
        await File.WriteAllTextAsync(path, "project=/p/P.csproj\nprofile=netprints.default\n", TestContext.Current.CancellationToken);
        try
        {
            Assert.Same(CSharpTarget.Unconstrained, GenerateRequestFile.Parse(path).Target);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ARequestCarriesTheFrameworkLines()
    {
        string path = Path.Combine(Path.GetTempPath(), $"netprints-rsp-{Guid.NewGuid():N}.rsp");
        await File.WriteAllTextAsync(path,
            "project=/p/P.csproj\nprofile=netprints.default\nframework=net10.0|latest|14.0\nframework=net8.0|11.0|12.0\n",
            TestContext.Current.CancellationToken);
        try
        {
            CSharpTarget target = GenerateRequestFile.Parse(path).Target;

            Assert.Equal(new CSharpLanguageVersion(11, 0), target.Version);
            Assert.Equal(["net10.0", "net8.0"], target.TargetFrameworks);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string PrepareFixture(string fixture)
    {
        string directory = Directory.CreateTempSubdirectory($"np-cstarget-{fixture}-").FullName;
        string source = Path.Combine(AppContext.BaseDirectory, "Fixtures", "CSharpTarget", fixture);
        foreach (string file in Directory.EnumerateFiles(source))
        {
            File.Copy(file, Path.Combine(directory, Path.GetFileName(file)));
        }

        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "CSharpTarget", GraphName), Path.Combine(directory, GraphName));
        LocalSdkLayout.Write(directory);
        return directory;
    }

    private static Task<ProjectSnapshot> LoadSnapshotAsync(string project) =>
        new MsBuildProjectSystem(new ProjectSystemOptions([], "9.9.9-test"), new ProcessRunner(), NullLogger<MsBuildProjectSystem>.Instance)
            .LoadAsync(project, TestContext.Current.CancellationToken);

    private static async Task<string> RenderLikeTheEditorAsync(ProjectSnapshot snapshot, string graphPath)
    {
        var registry = new NodeDocumentConverterRegistry(NodeDocumentConverterRegistry.BuiltIn, []);
        var mapper = new DocumentMapper(registry, NullLogger<DocumentMapper>.Instance);
        var format = new JsonDocumentFormat(new NetPrintsJsonOptions(registry), new DocumentMigrator([], NullLogger<DocumentMigrator>.Instance));
        var id = new DocumentId(Path.GetFileName(graphPath));
        await using FileStream input = File.OpenRead(graphPath);
        ClassDocument document = await format.ReadClassAsync(input, id, TestContext.Current.CancellationToken);
        ClassGraph cls = mapper.FromDocument(document, Project.FromSnapshot(snapshot), new List<DocumentIssue>(), id);
        return GraphCodeGenerator.RenderFile(new ClassTranslator(TranslationEnvironment.BuiltIn).Translate(cls), Path.GetFileName(graphPath));
    }
}
