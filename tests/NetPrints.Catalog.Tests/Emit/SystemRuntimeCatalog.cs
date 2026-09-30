using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace NetPrints.Catalog.Tests.Emit;

/// <summary>The catalog of the <c>System.Runtime</c> reference assembly, the size that reached CS8103 (D-R3).</summary>
internal static class SystemRuntimeCatalog
{
    private const string SystemRuntime = "System.Runtime";

    private static readonly Lazy<CatalogDocument> Document = new(Build);

    public static CatalogDocument WithId(string id) => Document.Value with { Id = id };

    private static CatalogDocument Build()
    {
        string directory = ReferenceAssemblyDirectory();
        MetadataReference[] references = [.. Directory.EnumerateFiles(directory, "*.dll").Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))];
        MetadataReference target = references.Single(reference => string.Equals(Path.GetFileNameWithoutExtension(reference.Display), SystemRuntime, StringComparison.Ordinal));
        CSharpCompilation compilation = FixtureCatalog.CreateCompilation("Tool", references);
        return CatalogBuilder.Build(
            compilation,
            [FixtureCatalog.AssemblyOf(compilation, target)],
            new CatalogProfileFilter(BuiltInCatalogProfiles.PublicApi),
            XmlDocumentationSource.FromText(File.ReadAllText(Path.Combine(directory, SystemRuntime + ".xml"))),
            new CatalogIdentity(SystemRuntime.ToLowerInvariant())).Document;
    }

    private static string ReferenceAssemblyDirectory()
    {
        string runtime = RuntimeEnvironment.GetRuntimeDirectory().TrimEnd(Path.DirectorySeparatorChar);
        string packs = Path.Combine(Path.GetFullPath(Path.Combine(runtime, "..", "..", "..")), "packs", "Microsoft.NETCore.App.Ref");
        IEnumerable<string> candidates = Directory.EnumerateDirectories(packs)
            .Select(version => Path.Combine(version, "ref", "net10.0"))
            .Where(path => File.Exists(Path.Combine(path, SystemRuntime + ".dll")))
            .Order(StringComparer.Ordinal);
        return candidates.LastOrDefault() ?? throw new InvalidOperationException($"No net10.0 reference assemblies under {packs}.");
    }
}
