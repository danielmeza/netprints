using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace NetPrints.Catalog.Tests.Engine;

/// <summary>
/// CT-T17 (SC-006): building the fixture catalog takes under 5 seconds and the <c>System.Runtime</c> reference assembly under 30.
/// The limits are the specified ones; measured locally the runs take a small fraction of them, so the headroom absorbs a slow CI runner.
/// </summary>
public sealed class CatalogPerformanceTests
{
    private const string SystemRuntime = "System.Runtime";
    private static readonly TimeSpan FixtureLimit = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SystemRuntimeLimit = TimeSpan.FromSeconds(30);

    [Fact]
    public void TheFixtureCatalogIsBuiltUnderFiveSeconds()
    {
        var watch = Stopwatch.StartNew();
        CatalogBuildResult result = FixtureCatalog.BuildFixture(BuiltInCatalogProfiles.PublicApi, FixtureCatalog.FixtureId);
        watch.Stop();

        Assert.NotEmpty(result.Document.Types);
        Assert.True(watch.Elapsed < FixtureLimit, $"The fixture catalog took {watch.Elapsed.TotalSeconds:F2} s (limit {FixtureLimit.TotalSeconds} s).");
    }

    [Fact]
    public void TheSystemRuntimeReferenceAssemblyIsCatalogedUnderThirtySeconds()
    {
        string directory = ReferenceAssemblyDirectory();
        var watch = Stopwatch.StartNew();
        MetadataReference[] references = [.. Directory.EnumerateFiles(directory, "*.dll").Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
        MetadataReference target = references.Single(reference => string.Equals(Path.GetFileNameWithoutExtension(reference.Display), SystemRuntime, StringComparison.Ordinal));
        CSharpCompilation compilation = FixtureCatalog.CreateCompilation("Tool", references);
        IAssemblySymbol assembly = FixtureCatalog.AssemblyOf(compilation, target);

        CatalogBuildResult result = CatalogBuilder.Build(
            compilation,
            [assembly],
            new CatalogProfileFilter(BuiltInCatalogProfiles.PublicApi),
            XmlDocumentationSource.FromText(File.ReadAllText(Path.Combine(directory, SystemRuntime + ".xml"))),
            new CatalogIdentity(SystemRuntime.ToLowerInvariant()));
        watch.Stop();

        Assert.Contains(result.Document.Types, type => type.Id == "T:System.String");
        Assert.True(watch.Elapsed < SystemRuntimeLimit, $"The System.Runtime catalog took {watch.Elapsed.TotalSeconds:F2} s (limit {SystemRuntimeLimit.TotalSeconds} s).");
    }

    // The reference pack of the running framework, next to the shared runtime: <root>/packs/Microsoft.NETCore.App.Ref/<version>/ref/net10.0.
    private static string ReferenceAssemblyDirectory()
    {
        string runtime = RuntimeEnvironment.GetRuntimeDirectory().TrimEnd(Path.DirectorySeparatorChar);
        string root = Path.GetFullPath(Path.Combine(runtime, "..", "..", ".."));
        string packs = Path.Combine(root, "packs", "Microsoft.NETCore.App.Ref");
        Assert.True(Directory.Exists(packs), $"No reference pack directory at {packs}.");
        IEnumerable<string> candidates = Directory.EnumerateDirectories(packs)
            .Select(version => Path.Combine(version, "ref", "net10.0"))
            .Where(path => File.Exists(Path.Combine(path, SystemRuntime + ".dll")))
            .Order(StringComparer.Ordinal);
        return candidates.LastOrDefault() ?? throw new Xunit.Sdk.XunitException($"No net10.0 reference assemblies under {packs}.");
    }
}
