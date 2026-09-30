extern alias Annotations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using AnnotationsGenerator = Annotations::NetPrints.Annotations.CatalogGenerator;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace NetPrints.Catalog.Tests.Generator;

/// <summary>An embedded catalog read back from the compiled assembly's <c>NetPrintsEmbeddedCatalogAttribute</c>.</summary>
internal sealed record EmbeddedCatalog(string Id, int SchemaVersion, string Json);

/// <summary>The additional files and build properties a generator run sees (what the MSBuild targets of the package provide).</summary>
internal sealed class GeneratorInputs
{
    private const string DocumentationMetadata = "build_metadata.AdditionalFiles.NetPrintsReferenceDocumentation";

    private const string RootNamespaceProperty = "build_property.RootNamespace";

    private readonly List<InMemoryText> files = [];

    public string? RootNamespace { get; init; }

    public GeneratorInputs AddFile(string path, string text) => Add(new InMemoryText(path, text, isDocumentation: false));

    public GeneratorInputs AddDocumentation(string path, string text) => Add(new InMemoryText(path, text, isDocumentation: true));

    public ImmutableArray<AdditionalText> Texts => [.. files];

    public AnalyzerConfigOptionsProvider Options() => new InMemoryOptionsProvider(this);

    private GeneratorInputs Add(InMemoryText text)
    {
        files.Add(text);
        return this;
    }

    private sealed class InMemoryText(string path, string text, bool isDocumentation) : AdditionalText
    {
        public override string Path { get; } = path;

        public bool IsDocumentation { get; } = isDocumentation;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }

    private sealed class InMemoryOptions(IReadOnlyDictionary<string, string> values) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value) => values.TryGetValue(key, out value);
    }

    private sealed class InMemoryOptionsProvider(GeneratorInputs inputs) : AnalyzerConfigOptionsProvider
    {
        private static readonly InMemoryOptions None = new(new Dictionary<string, string>());

        public override AnalyzerConfigOptions GlobalOptions { get; } =
            inputs.RootNamespace is null ? None : new InMemoryOptions(new Dictionary<string, string> { [RootNamespaceProperty] = inputs.RootNamespace });

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => None;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
            textFile is InMemoryText { IsDocumentation: true } ? new InMemoryOptions(new Dictionary<string, string> { [DocumentationMetadata] = "true" }) : None;
    }
}

/// <summary>Runs <see cref="AnnotationsGenerator"/> over in-memory sources against reference assemblies.</summary>
internal static class GeneratorTestHost
{
    private const string EmbeddedCatalogAttributeName = "NetPrintsEmbeddedCatalogAttribute";

    private static readonly ImmutableArray<MetadataReference> FrameworkReferences = LoadFrameworkReferences();

    public static CSharpCompilation Compile(string assemblyName, string source, params MetadataReference[] extraReferences) =>
        Compile(assemblyName, [source], LanguageVersion.Latest, FrameworkReferences.AddRange(extraReferences));

    public static CSharpCompilation Compile(string assemblyName, IEnumerable<string> sources, LanguageVersion languageVersion, IEnumerable<MetadataReference> references) =>
        CSharpCompilation.Create(
            assemblyName,
            sources.Select(source => CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(languageVersion))),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: languageVersion == LanguageVersion.CSharp7_3 ? NullableContextOptions.Disable : NullableContextOptions.Enable));

    public static GeneratorDriver CreateDriver(CSharpCompilation compilation, GeneratorInputs? inputs = null, bool trackSteps = false)
    {
        inputs ??= new GeneratorInputs();
        CSharpParseOptions parseOptions = (CSharpParseOptions)(compilation.SyntaxTrees.FirstOrDefault()?.Options ?? CSharpParseOptions.Default);
        return CSharpGeneratorDriver.Create(
            [new AnnotationsGenerator().AsSourceGenerator()],
            inputs.Texts,
            parseOptions,
            inputs.Options(),
            new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackSteps));
    }

    public static (RoslynCompilation Output, ImmutableArray<Diagnostic> GeneratorDiagnostics) Run(CSharpCompilation compilation, GeneratorInputs? inputs = null)
    {
        GeneratorDriver driver = CreateDriver(compilation, inputs);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out RoslynCompilation output, out ImmutableArray<Diagnostic> diagnostics);
        return (output, diagnostics);
    }

    public static IReadOnlyList<Diagnostic> Errors(RoslynCompilation compilation) =>
        compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

    public static IReadOnlyList<EmbeddedCatalog> EmbeddedCatalogs(RoslynCompilation output) =>
        [
            .. output.Assembly.GetAttributes()
                .Where(a => a.AttributeClass?.Name == EmbeddedCatalogAttributeName)
                .Select(a => new EmbeddedCatalog((string)(a.ConstructorArguments[0].Value ?? string.Empty), (int)(a.ConstructorArguments[1].Value ?? 0), (string)(a.ConstructorArguments[2].Value ?? string.Empty)))
                .OrderBy(c => c.Id, StringComparer.Ordinal),
        ];

    public static IReadOnlyList<(string HintName, string Text)> GeneratedSources(GeneratorDriver driver) =>
        [.. driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources).Select(s => (s.HintName, s.SourceText.ToString())).OrderBy(s => s.HintName, StringComparer.Ordinal)];

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
