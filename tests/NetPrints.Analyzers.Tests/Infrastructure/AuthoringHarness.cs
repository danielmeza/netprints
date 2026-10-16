using System;
using System.Collections.Immutable;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Graph;
using Xunit;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace NetPrints.Analyzers.Tests.Infrastructure;

/// <summary>Runs the authoring generators over samples compiled against the framework and the real <c>NetPrints.Core</c> (ADR-0024: the generators load from their output folder).</summary>
internal static class AuthoringHarness
{
    private const string Authoring = "NetPrints.Authoring";

    internal static readonly ImmutableArray<MetadataReference> References =
    [
        .. GeneratorHarness.References,
        MetadataReference.CreateFromFile(typeof(Node).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(ObservableObject).Assembly.Location),
    ];

    private static int assemblyNumber;

    /// <summary>Compiles sources as a library.</summary>
    /// <param name="sources">The sources.</param>
    /// <returns>The compilation, without generator output.</returns>
    public static CSharpCompilation Compile(params string[] sources) =>
        CSharpCompilation.Create(
            "AuthoringSample" + System.Threading.Interlocked.Increment(ref assemblyNumber),
            sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, path: "Sample" + index + ".cs", cancellationToken: TestContext.Current.CancellationToken)),
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    /// <summary>Creates a driver with every generator of the authoring analyzers.</summary>
    /// <param name="trackSteps">Whether to track the incremental steps.</param>
    /// <returns>The driver.</returns>
    public static GeneratorDriver CreateDriver(bool trackSteps = false) =>
        CSharpGeneratorDriver.Create(
            Generators(),
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackSteps));

    /// <summary>Runs the generators over sources.</summary>
    /// <param name="sources">The sources.</param>
    /// <returns>The run.</returns>
    public static GeneratorRun Run(params string[] sources)
    {
        CSharpCompilation compilation = Compile(sources);
        GeneratorDriver driver = CreateDriver().RunGeneratorsAndUpdateCompilation(compilation, out RoslynCompilation output, out ImmutableArray<Diagnostic> diagnostics, TestContext.Current.CancellationToken);
        return new GeneratorRun(output, diagnostics, GeneratorHarness.Sources(driver));
    }

    private static ImmutableArray<ISourceGenerator> Generators() =>
    [
        .. AnalyzerBinaries.Load(Authoring).GetTypes()
            .Where(type => !type.IsAbstract && typeof(IIncrementalGenerator).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => Assert.IsType<IIncrementalGenerator>(Activator.CreateInstance(type), exactMatch: false).AsSourceGenerator()),
    ];
}
