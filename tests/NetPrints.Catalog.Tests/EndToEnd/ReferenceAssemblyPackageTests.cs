using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Catalog.Tests.EndToEnd;

/// <summary>
/// AN-T15: a library packed with its reference assembly under <c>ref/net10.0/</c> (as well as <c>lib/net10.0/</c>) still exposes its embedded catalog
/// through the assembly a consumer resolves, which is the reference assembly. The library is a throwaway <c>net10.0</c> project built from the
/// <c>AnnotatedSample</c> sources against a temporary feed holding the packed <c>NetPrints.Annotations</c>.
/// </summary>
public sealed class ReferenceAssemblyPackageTests(ReferenceAssemblyPackage package) : IClassFixture<ReferenceAssemblyPackage>
{
    [Fact]
    public void ThePackageCarriesTheReferenceAssemblyAndTheImplementation()
    {
        Assert.Contains("ref/net10.0/AnnotatedRef.dll", package.PackageEntries);
        Assert.Contains("lib/net10.0/AnnotatedRef.dll", package.PackageEntries);
    }

    [Fact]
    public void TheConsumerResolvesTheReferenceAssembly() =>
        Assert.Contains("/ref/net10.0/", package.ResolvedReferencePath.Replace('\\', '/'), StringComparison.Ordinal);

    [Fact]
    public void TheCatalogIsFoundInTheResolvedReferenceAssembly()
    {
        CatalogDocument document = Assert.Single(EmbeddedCatalogReader.Read(package.ResolvedReferencePath));

        Assert.Equal("annotatedref", document.Id);
        Assert.Contains(document.Types, type => type.Id == "T:AnnotatedSample.Greeter");
    }
}

/// <summary>Packs <c>NetPrints.Annotations</c> and a <c>net10.0</c> library with a reference assembly into a temporary feed, and restores a consumer of the library, once per test class.</summary>
public sealed class ReferenceAssemblyPackage : IAsyncLifetime
{
    private const string PackageId = "AnnotatedRef";

    private static readonly string Configuration =
#if DEBUG
        "Debug";
#else
        "Release";
#endif

    private readonly string root = Directory.CreateTempSubdirectory("np-refasm-").FullName;

    private readonly ProcessRunner runner = new();

    public string ResolvedReferencePath { get; private set; } = string.Empty;

    public string[] PackageEntries { get; private set; } = [];

    private string Feed => Path.Combine(root, "feed");

    public async ValueTask InitializeAsync()
    {
        Directory.CreateDirectory(Feed);
        string annotations = Path.Combine(TestPaths.RepositoryRoot(), "src", "NetPrints.Annotations", "NetPrints.Annotations.csproj");
        await RunAsync(TestPaths.RepositoryRoot(), "pack", annotations, "--no-build", "-nodeReuse:false", "-c", Configuration, "-o", Feed, "--nologo", "-v", "q");
        string version = Path.GetFileNameWithoutExtension(Directory.EnumerateFiles(Feed, "NetPrints.Annotations.*.nupkg").Single())["NetPrints.Annotations.".Length..];

        string library = Path.Combine(root, "library");
        Directory.CreateDirectory(library);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "EndToEnd", "AnnotatedSample", "Greeter.cs"), Path.Combine(library, "Greeter.cs"));
        File.WriteAllText(Path.Combine(library, PackageId + ".csproj"), LibraryProject(version));
        IsolatedNuGetConfig.Write(library, Feed, Path.Combine(root, "packages"), "NetPrints.*");
        await RunAsync(library, "pack", "-nodeReuse:false", "-c", Configuration, "-o", Feed, "--nologo", "-v", "q");

        using (ZipArchive archive = ZipFile.OpenRead(Path.Combine(Feed, PackageId + ".1.0.0.nupkg")))
        {
            PackageEntries = [.. archive.Entries.Select(entry => entry.FullName)];
        }

        string consumer = Path.Combine(root, "consumer");
        Directory.CreateDirectory(consumer);
        File.WriteAllText(Path.Combine(consumer, "Consumer.csproj"), ConsumerProject);
        File.WriteAllText(Path.Combine(consumer, "Class1.cs"), "public sealed class Class1 { public string Say() => AnnotatedSample.Greeter.Greet(\"x\"); }");
        IsolatedNuGetConfig.Write(consumer, Feed, Path.Combine(root, "packages"), PackageId);
        await RunAsync(consumer, "restore", "-nodeReuse:false", "--nologo", "-v", "q");
        string items = await RunAsync(consumer, "msbuild", "-nodeReuse:false", "-t:ResolveAssemblyReferences", "-getItem:ReferencePath", "-p:Configuration=" + Configuration);
        ResolvedReferencePath = ReferencePaths(items).Single(path => Path.GetFileName(path) == PackageId + ".dll");
    }

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    private static string[] ReferencePaths(string json)
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
        return
        [
            .. document.RootElement.GetProperty("Items").GetProperty("ReferencePath").EnumerateArray()
                .Select(item => item.GetProperty("FullPath").GetString() ?? string.Empty),
        ];
    }

    private async Task<string> RunAsync(string workingDirectory, params string[] arguments)
    {
        ProcessResult result = await runner.RunAsync(new ProcessStartRequest("dotnet", arguments, workingDirectory), CancellationToken.None);
        Assert.True(result.ExitCode == 0, $"dotnet {string.Join(' ', arguments)} failed:\n{result.StandardOutput}\n{result.StandardError}");
        return result.StandardOutput;
    }

    private static string LibraryProject(string version) => $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
            <Version>1.0.0</Version>
            <PackageId>{PackageId}</PackageId>
            <GenerateDocumentationFile>true</GenerateDocumentationFile>
            <ProduceReferenceAssembly>true</ProduceReferenceAssembly>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="NetPrints.Annotations" Version="{version}" PrivateAssets="all" />
          </ItemGroup>
          <Target Name="PackReferenceAssembly" BeforeTargets="_GetPackageFiles">
            <ItemGroup>
              <None Include="@(IntermediateRefAssembly->'%(FullPath)')" Pack="true" PackagePath="ref/$(TargetFramework)" />
            </ItemGroup>
          </Target>
        </Project>
        """;

    private const string ConsumerProject = $"""
        <Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <TargetFramework>net10.0</TargetFramework>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="{PackageId}" Version="1.0.0" />
          </ItemGroup>
        </Project>
        """;
}
