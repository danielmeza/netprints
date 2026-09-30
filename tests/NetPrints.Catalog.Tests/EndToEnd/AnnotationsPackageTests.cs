using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Catalog.Tests.EndToEnd;

/// <summary>
/// AN-T10: the packed <c>NetPrints.Annotations</c> package works for a real netstandard2.0 consumer. It is packed into a temporary feed and a
/// throwaway <c>AnnotatedSample</c> project (version 1.0.0, in a temporary directory outside the repository) is built against it; the catalog
/// it embeds is the committed snapshot. The build restores <c>NETStandard.Library</c> from nuget.org; <c>NetPrints.*</c> comes only from the feed.
/// </summary>
public sealed class AnnotationsPackageTests(AnnotatedSampleBuild build) : IClassFixture<AnnotatedSampleBuild>
{
    [Fact]
    public void TheEmbeddedCatalogIsTheSnapshot()
    {
        string actual = CanonicalCatalogWriter.Write(Assert.Single(EmbeddedCatalogReader.Read(build.AssemblyPath)));
        string path = TestPaths.SnapshotPath("annotated-sample.npcat.json");
        if (TestPaths.UpdateSnapshots)
        {
            File.WriteAllText(path, actual);
        }

        Assert.True(File.Exists(path), $"Missing snapshot {path}; regenerate with {TestPaths.UpdateSnapshotsVariable}=1");
        Assert.Equal(File.ReadAllText(path), actual);
    }

    [Fact]
    public void TheConsumerBuildsWithoutWarnings() =>
        Assert.DoesNotMatch(new Regex(@"warning [A-Z]+\d+", RegexOptions.None, TimeSpan.FromSeconds(5)), build.BuildOutput);

    [Fact]
    public void TheConsumerAssemblyReferencesNoNetPrintsAssembly() =>
        Assert.DoesNotContain(
            ReferencedNames(build.AssemblyPath),
            name => name.StartsWith("NetPrints", StringComparison.Ordinal));

    [Fact]
    public void ThePackageBuildFileMakesTheReferenceDocumentationFlagVisibleToTheCompiler() =>
        Assert.Contains("NetPrintsReferenceDocumentation", build.CompilerVisibleItemMetadata);

    private static IReadOnlyList<string> ReferencedNames(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using PEReader reader = new(stream);
        MetadataReader metadata = reader.GetMetadataReader();
        return [.. metadata.AssemblyReferences.Select(h => metadata.GetString(metadata.GetAssemblyReference(h).Name))];
    }
}

/// <summary>Packs <c>NetPrints.Annotations</c> into a temporary feed and builds the sample against it, once per test class.</summary>
public sealed class AnnotatedSampleBuild : IAsyncLifetime
{
    private static readonly string Configuration =
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    private readonly string root = Directory.CreateTempSubdirectory("np-annotated-").FullName;

    private readonly ProcessRunner runner = new();

    public string AssemblyPath => Path.Combine(root, "sample", "bin", Configuration, "netstandard2.0", "AnnotatedSample.dll");

    public string BuildOutput { get; private set; } = string.Empty;

    public string CompilerVisibleItemMetadata { get; private set; } = string.Empty;

    private string Feed => Path.Combine(root, "feed");

    private string SampleDirectory => Path.Combine(root, "sample");

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(Feed);
        string annotations = Path.Combine(TestPaths.RepositoryRoot(), "src", "NetPrints.Annotations", "NetPrints.Annotations.csproj");
        await RunAsync(TestPaths.RepositoryRoot(), "pack", annotations, "--no-build", "-nodeReuse:false", "-c", Configuration, "-o", Feed, "--nologo", "-v", "q");

        string package = Directory.EnumerateFiles(Feed, "NetPrints.Annotations.*.nupkg").Single();
        string version = Path.GetFileNameWithoutExtension(package)["NetPrints.Annotations.".Length..];

        CopySources();
        File.WriteAllText(Path.Combine(SampleDirectory, "AnnotatedSample.csproj"), Project(version));
        IsolatedNuGetConfig.Write(SampleDirectory, Feed, Path.Combine(root, "packages"), "NetPrints.*");

        BuildOutput = await RunAsync(SampleDirectory, "build", "-nodeReuse:false", "-c", Configuration, "--nologo", "-v", "q");
        CompilerVisibleItemMetadata = await RunAsync(SampleDirectory, "msbuild", "-nodeReuse:false", "-getItem:CompilerVisibleItemMetadata");
    }

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    private async Task<string> RunAsync(string workingDirectory, params string[] arguments)
    {
        ProcessResult result = await runner.RunAsync(new ProcessStartRequest("dotnet", arguments, workingDirectory), CancellationToken.None);
        Assert.True(result.ExitCode == 0, $"dotnet {string.Join(' ', arguments)} failed:\n{result.StandardOutput}\n{result.StandardError}");
        return result.StandardOutput + result.StandardError;
    }

    private void CopySources()
    {
        Directory.CreateDirectory(SampleDirectory);
        foreach (string file in Directory.EnumerateFiles(Path.Combine(AppContext.BaseDirectory, "EndToEnd", "AnnotatedSample"), "*.cs"))
        {
            File.Copy(file, Path.Combine(SampleDirectory, Path.GetFileName(file)));
        }
    }

    private static string Project(string version) => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>netstandard2.0</TargetFramework>
            <Version>1.0.0</Version>
            <GenerateDocumentationFile>true</GenerateDocumentationFile>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="NetPrints.Annotations" Version="{version}" PrivateAssets="all" />
          </ItemGroup>
        </Project>
        """;
}
