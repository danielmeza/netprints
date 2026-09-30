extern alias Annotations;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;
using AnnotationsGenerator = Annotations::NetPrints.Annotations.CatalogGenerator;

namespace NetPrints.Catalog.Tests.Generator;

/// <summary>Runs <see cref="AnnotationsGenerator"/> over in-memory sources against the running framework's reference assemblies.</summary>
internal static class GeneratorTestHost
{
    private static readonly ImmutableArray<MetadataReference> FrameworkReferences = LoadFrameworkReferences();

    public static CSharpCompilation Compile(string assemblyName, string source, params MetadataReference[] extraReferences) =>
        CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Latest))],
            FrameworkReferences.AddRange(extraReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    public static (RoslynCompilation Output, ImmutableArray<Diagnostic> GeneratorDiagnostics) Run(CSharpCompilation compilation)
    {
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new AnnotationsGenerator());
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out RoslynCompilation output, out ImmutableArray<Diagnostic> diagnostics);
        return (output, diagnostics);
    }

    public static IReadOnlyList<Diagnostic> Errors(RoslynCompilation compilation) =>
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

    public static byte[] Emit(RoslynCompilation compilation)
    {
        using MemoryStream stream = new();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(stream);
        return result.Success ? stream.ToArray() : throw new InvalidDataException(string.Join('\n', result.Diagnostics));
    }

    private static ImmutableArray<MetadataReference> LoadFrameworkReferences()
    {
        string? trusted = (string?)System.AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES");
        return (trusted ?? string.Empty)
            .Split(Path.PathSeparator, System.StringSplitOptions.RemoveEmptyEntries)
            .Where(p => Path.GetFileName(p) is "System.Runtime.dll" or "System.Private.CoreLib.dll" or "System.Collections.dll" or "netstandard.dll")
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .ToImmutableArray();
    }
}
