using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Projects;

namespace NetPrints.Catalog;

/// <summary>What <see cref="CatalogBuilder"/> needs to catalog resolved sources.</summary>
/// <param name="Compilation">A compilation over the references, without syntax trees.</param>
/// <param name="Assemblies">The symbols of the assemblies to catalog.</param>
/// <param name="Documentation">The documentation of those assemblies.</param>
public sealed record CatalogCompilationInput(CSharpCompilation Compilation, IReadOnlyList<IAssemblySymbol> Assemblies, IDocumentationSource Documentation);

/// <summary>Creates the compilation of a catalog run from resolved sources.</summary>
public static class CatalogCompilationFactory
{
    /// <summary>Opens the assemblies of <paramref name="sources"/> as metadata references.</summary>
    /// <param name="sources">The resolved sources.</param>
    /// <returns>The compilation (with non-public metadata imported, so annotations on internal members are seen), the assembly symbols and the documentation.</returns>
    /// <exception cref="CatalogSourceException">An assembly cannot be read or has no symbol.</exception>
    public static CatalogCompilationInput Create(CatalogSourceSet sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        Dictionary<string, MetadataReference> references = new(StringComparer.Ordinal);
        foreach (ResolvedAssembly assembly in sources.References.Concat(sources.Targets))
        {
            if (!references.ContainsKey(assembly.Path))
            {
                references.Add(assembly.Path, Open(assembly.Path));
            }
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            "NetPrintsCatalog",
            syntaxTrees: null,
            references.Values,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, metadataImportOptions: MetadataImportOptions.All));

        List<IAssemblySymbol> assemblies = [];
        List<string> documentation = [];
        foreach (ResolvedAssembly target in sources.Targets.DistinctBy(target => target.Path, StringComparer.Ordinal))
        {
            assemblies.Add(compilation.GetAssemblyOrModuleSymbol(references[target.Path]) as IAssemblySymbol
                ?? throw new CatalogSourceException($"'{target.Path}' is not an assembly."));
            if (target.DocumentationPath is not null && File.Exists(target.DocumentationPath))
            {
                documentation.Add(File.ReadAllText(target.DocumentationPath));
            }
        }

        return new CatalogCompilationInput(compilation, assemblies, new XmlDocumentationSource(documentation));
    }

    private static MetadataReference Open(string path)
    {
        try
        {
            return MetadataReference.CreateFromFile(path);
        }
        catch (Exception exception) when (exception is IOException or BadImageFormatException or UnauthorizedAccessException)
        {
            throw new CatalogSourceException($"Cannot read the assembly '{path}': {exception.Message}", exception);
        }
    }
}
