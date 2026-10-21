using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Cli.Tests.Support;
using NetPrints.Extensibility;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Commands;

/// <summary>US10 scenario 3, FR-080: <c>netprints extension verify</c> runs NPK001 to NPK003 and NPK017 on a folder or a package, loading nothing.</summary>
public sealed class ExtensionVerifyCommandTests : IDisposable
{
    private const string ExtensionAssembly = "Fx.Verify.dll";

    private const string ProgramSource = """
        using NetPrints.Core;
        namespace Fx.Verify;
        public static class Program
        {
            public static void Run()
            {
                var widget = new Widget();
                widget.Present();
                widget.Missing();
                widget.Present(1);
                var gone = new Gone();
            }
        }
        """;

    private readonly string _directory = Directory.CreateTempSubdirectory("np-verify-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    private static string Api() => $"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}";

    private static string Manifest(string api) =>
        $$"""{ "id": "fx.verify", "name": "Verify fixture", "version": "1.0.0", "assembly": "{{ExtensionAssembly}}", "netprintsApi": "{{api}}", "dependsOn": [] }""";

    private string Folder(string name)
    {
        string path = Path.Combine(_directory, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static void Compile(string folder, string assemblyName, string source, params string[] references)
    {
        IEnumerable<MetadataReference> platform = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
            .Split(Path.PathSeparator)
            .Where(path => Path.GetFileName(path) is "System.Private.CoreLib.dll" or "System.Runtime.dll" or "System.Console.dll")
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            [.. platform, .. references.Select(path => MetadataReference.CreateFromFile(path))],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var emit = compilation.Emit(Path.Combine(folder, assemblyName + ".dll"));
        Assert.True(emit.Success, string.Join('\n', emit.Diagnostics));
    }

    private static string SourceOfHost(bool withMissing) => $$"""
        namespace NetPrints.Core;
        public class Widget
        {
            public void Present() { }
            {{(withMissing ? "public void Present(int value) { } public void Missing() { }" : "public void Present(string value) { }")}}
        }
        {{(withMissing ? "public class Gone { }" : string.Empty)}}
        """;

    private (string Extension, string Host) IncompatibleExtension()
    {
        string stub = Folder("stub");
        Compile(stub, "NetPrints.Core", SourceOfHost(withMissing: true));
        string extension = Folder("incompatible");
        Compile(extension, "Fx.Verify", ProgramSource, Path.Combine(stub, "NetPrints.Core.dll"));
        File.WriteAllText(Path.Combine(extension, "netprints-extension.json"), Manifest(Api()));
        string host = Folder("host");
        Compile(host, "NetPrints.Core", SourceOfHost(withMissing: false));
        return (extension, host);
    }

    private string Package(string folder, string name, bool withType = true)
    {
        string path = Path.Combine(_directory, name + ".nupkg");
        using ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create);
        string types = withType ? """<packageTypes><packageType name="NetPrintsExtension" /></packageTypes>""" : string.Empty;
        using (var writer = new StreamWriter(archive.CreateEntry("Fx.nuspec").Open()))
        {
            writer.Write($"""<?xml version="1.0"?><package xmlns="http://schemas.microsoft.com/packaging/2013/05/nuspec.xsd"><metadata><id>Fx</id><version>1.0.0</version>{types}</metadata></package>""");
        }

        foreach (string file in Directory.EnumerateFiles(folder))
        {
            archive.CreateEntryFromFile(file, "extension/" + Path.GetFileName(file));
        }

        return path;
    }

    private async Task<(int Exit, string Output, string Error)> RunAsync(params string[] args)
    {
        var host = new CliTestHost(_directory);
        int exit = await host.RunAsync(["extension", "verify", .. args]);
        return (exit, host.Output, host.Error.ToString());
    }

    [Fact]
    public async Task AFolderThatFollowsTheRulesExitsZero()
    {
        (int exit, string output, string error) = await RunAsync(FixtureExtensions.EditorFolder());

        Assert.True(exit == 0, output + error);
        Assert.Contains("NPK001", output, StringComparison.Ordinal);
        Assert.Contains("passed", output, StringComparison.Ordinal);
        Assert.DoesNotContain("failed", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task APackageThatFollowsTheRulesExitsZero()
    {
        string package = Package(FixtureExtensions.EditorFolder(), "good");

        (int exit, string output, string error) = await RunAsync(package);

        Assert.True(exit == 0, output + error);
    }

    [Fact]
    public async Task APackageWithoutThePackageTypeFailsNpk002()
    {
        string package = Package(FixtureExtensions.EditorFolder(), "untyped", withType: false);

        (int exit, string output, _) = await RunAsync(package);

        Assert.Equal(1, exit);
        Assert.Contains("NPK002", output, StringComparison.Ordinal);
        Assert.Contains("NetPrintsExtension", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnApiVersionTheHostDoesNotImplementFailsNpk001()
    {
        string folder = Folder("manifest");
        File.WriteAllText(Path.Combine(folder, "netprints-extension.json"), Manifest("9.0"));

        (int exit, string output, _) = await RunAsync(folder);

        Assert.Equal(1, exit);
        Assert.Contains("NPK001 Manifest: failed", output, StringComparison.Ordinal);
        Assert.Contains("netprintsApi 9.0", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AMissingAssemblyFailsNpk002()
    {
        string folder = Folder("packaging");
        File.WriteAllText(Path.Combine(folder, "netprints-extension.json"), Manifest(Api()));

        (int exit, string output, _) = await RunAsync(folder);

        Assert.Equal(1, exit);
        Assert.Contains("NPK002 Packaging: failed", output, StringComparison.Ordinal);
        Assert.Contains(ExtensionAssembly, output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AReferenceToANetPrintsAssemblyOutsideTheContractFailsNpk003()
    {
        string stub = Folder("private-stub");
        Compile(stub, "NetPrints.Private", "namespace NetPrints.Private; public class Thing { }");
        string folder = Folder("identity");
        Compile(folder, "Fx.Verify", "namespace Fx.Verify; public class P { public object O = new NetPrints.Private.Thing(); }", Path.Combine(stub, "NetPrints.Private.dll"));
        File.WriteAllText(Path.Combine(folder, "netprints-extension.json"), Manifest(Api()));

        (int exit, string output, _) = await RunAsync(folder);

        Assert.Equal(1, exit);
        Assert.Contains("NPK003 Type identity: failed", output, StringComparison.Ordinal);
        Assert.Contains("NetPrints.Private", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EachMemberTheHostLacksIsListedUnderNpk017()
    {
        (string extension, string host) = IncompatibleExtension();

        (int exit, string output, _) = await RunAsync(extension, "--host", host);

        Assert.Equal(1, exit);
        Assert.Contains("NPK017 API compatibility: failed", output, StringComparison.Ordinal);
        Assert.Contains("NetPrints.Core: NetPrints.Core.Widget.Missing() (referenced from Fx.Verify.Program)", output, StringComparison.Ordinal);
        Assert.Contains("NetPrints.Core: NetPrints.Core.Widget.Present(Int32) (referenced from Fx.Verify.Program)", output, StringComparison.Ordinal);
        Assert.Contains("NetPrints.Core: NetPrints.Core.Gone (referenced from Fx.Verify.Program)", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Widget.Present() ", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task JsonOutputFollowsTheContract()
    {
        (string extension, string host) = IncompatibleExtension();

        (int exit, string output, _) = await RunAsync(extension, "--host", host, "--format", "json");

        Assert.Equal(1, exit);
        using JsonDocument document = JsonDocument.Parse(output);
        JsonElement root = document.RootElement;
        Assert.Equal(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.Equal("fx.verify", root.GetProperty("extension").GetProperty("id").GetString());
        Assert.Equal(Api(), root.GetProperty("extension").GetProperty("netprintsApi").GetString());
        Assert.Equal(Api(), root.GetProperty("host").GetProperty("apiVersion").GetString());
        Assert.Contains("NetPrints.Core", root.GetProperty("host").GetProperty("assemblies").EnumerateArray().Select(item => item.GetString()));
        JsonElement[] checks = [.. root.GetProperty("checks").EnumerateArray()];
        Assert.Equal(["NPK001", "NPK002", "NPK003", "NPK017"], checks.Select(item => item.GetProperty("id").GetString()));
        JsonElement api = checks[3];
        Assert.Equal("apiCompatibility", api.GetProperty("name").GetString());
        Assert.Equal("failed", api.GetProperty("status").GetString());
        JsonElement[] missing = [.. root.GetProperty("missingMembers").EnumerateArray()];
        Assert.Equal(3, missing.Length);
        Assert.Contains(missing, item => item.GetProperty("type").GetString() == "NetPrints.Core.Widget" && item.GetProperty("member").GetString() == "Missing()");
        Assert.All(missing, item => Assert.Equal("Fx.Verify.Program", item.GetProperty("referencedFrom").GetString()));
    }

    [Fact]
    public async Task APathThatDoesNotExistExitsTwo()
    {
        (int exit, _, string error) = await RunAsync(Path.Combine(_directory, "nope"));

        Assert.Equal(2, exit);
        Assert.Contains("nope", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFileThatIsNotAPackageExitsTwo()
    {
        string file = Path.Combine(_directory, "notes.txt");
        await File.WriteAllTextAsync(file, "x", TestContext.Current.CancellationToken);

        (int exit, _, _) = await RunAsync(file);

        Assert.Equal(2, exit);
    }

    [Fact]
    public async Task AFolderWithoutAManifestFailsNpk001()
    {
        (int exit, string output, _) = await RunAsync(Folder("empty"));

        Assert.Equal(1, exit);
        Assert.Contains("NPK001 Manifest: failed", output, StringComparison.Ordinal);
        Assert.Contains("NPK017 API compatibility: skipped", output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnUnknownFormatOrHostFolderExitsTwo()
    {
        Assert.Equal(2, (await RunAsync(FixtureExtensions.EditorFolder(), "--format", "xml")).Exit);
        Assert.Equal(2, (await RunAsync(FixtureExtensions.EditorFolder(), "--host", Path.Combine(_directory, "no-host"))).Exit);
    }
}
