using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace NetPrints.Analyzers.Tests.Infrastructure;

/// <summary>The result of one generator run over a sample.</summary>
/// <param name="Output">The compilation with the generated sources added.</param>
/// <param name="Diagnostics">The diagnostics the generators reported.</param>
/// <param name="Sources">The generated sources by hint name.</param>
internal sealed record GeneratorRun(RoslynCompilation Output, ImmutableArray<Diagnostic> Diagnostics, IReadOnlyDictionary<string, string> Sources)
{
    /// <summary>Gets the generated source with a hint name.</summary>
    /// <param name="hintName">The hint name.</param>
    /// <returns>The text.</returns>
    public string Source(string hintName) => Sources[hintName];

    /// <summary>Gets the compile errors of the output, generated sources included.</summary>
    /// <returns>The errors.</returns>
    public IReadOnlyList<Diagnostic> CompileErrors() =>
        [.. Output.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
}

/// <summary>Runs the repository generators over samples compiled against the framework, MS.DI and logging (ADR-0024: the generators load from their output folder).</summary>
internal static class GeneratorHarness
{
    private const string RepoAnalyzers = "NetPrints.RepoAnalyzers";

    internal static readonly ImmutableArray<MetadataReference> References = LoadReferences();

    private static int assemblyNumber;

    /// <summary>Compiles sources as a library.</summary>
    /// <param name="sources">The sources.</param>
    /// <returns>The compilation, without generator output.</returns>
    public static CSharpCompilation Compile(params string[] sources) =>
        CSharpCompilation.Create(
            "Sample" + System.Threading.Interlocked.Increment(ref assemblyNumber),
            sources.Select((source, index) => CSharpSyntaxTree.ParseText(source, path: "Sample" + index + ".cs", cancellationToken: TestContext.Current.CancellationToken)),
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

    /// <summary>Creates a driver with every generator of the repository analyzers.</summary>
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
        return new GeneratorRun(output, diagnostics, Sources(driver));
    }

    /// <summary>Gets the generated sources of a driver by hint name.</summary>
    /// <param name="driver">The driver after a run.</param>
    /// <returns>The sources.</returns>
    public static IReadOnlyDictionary<string, string> Sources(GeneratorDriver driver) =>
        driver.GetRunResult().Results.SelectMany(result => result.GeneratedSources).ToDictionary(source => source.HintName, source => source.SourceText.ToString(), StringComparer.Ordinal);

    /// <summary>Asserts that the generators report exactly the marked spans for the given ids.</summary>
    /// <param name="markup">The source with <c>{|ID:text|}</c> markers.</param>
    /// <param name="ids">The ids to check.</param>
    public static void AssertDiagnostics(string markup, params string[] ids)
    {
        (string source, IReadOnlyList<AnalyzerHarness.Expected> expected) = AnalyzerHarness.Parse(markup);
        GeneratorRun run = Run(source);
        AnalyzerHarness.Expected[] reported = [.. run.Diagnostics.Where(diagnostic => ids.Contains(diagnostic.Id, StringComparer.Ordinal))
            .Select(diagnostic => new AnalyzerHarness.Expected(diagnostic.Id, diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length))
            .OrderBy(item => item.Start).ThenBy(item => item.Id, StringComparer.Ordinal)];

        Assert.Equal(expected, reported);
    }

    /// <summary>Emits a compilation and loads it.</summary>
    /// <param name="compilation">The compilation to emit.</param>
    /// <returns>The loaded assembly.</returns>
    public static Assembly EmitAndLoad(RoslynCompilation compilation)
    {
        using MemoryStream stream = new();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken);
        Assert.True(result.Success, string.Join('\n', result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
        return Assembly.Load(stream.ToArray());
    }

    private static ImmutableArray<ISourceGenerator> Generators() =>
    [
        .. AnalyzerBinaries.Load(RepoAnalyzers).GetTypes()
            .Where(type => !type.IsAbstract && typeof(IIncrementalGenerator).IsAssignableFrom(type))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => Assert.IsType<IIncrementalGenerator>(Activator.CreateInstance(type), exactMatch: false).AsSourceGenerator()),
    ];

    private static ImmutableArray<MetadataReference> LoadReferences()
    {
        string trusted = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        return
        [
            .. trusted.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
                .Where(path => IsReference(Path.GetFileName(path)))
                .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)),
        ];
    }

    private static bool IsReference(string fileName) =>
        fileName.StartsWith("System.", StringComparison.Ordinal)
        || fileName.StartsWith("Microsoft.Extensions.", StringComparison.Ordinal)
        || fileName is "netstandard.dll" or "mscorlib.dll";
}
