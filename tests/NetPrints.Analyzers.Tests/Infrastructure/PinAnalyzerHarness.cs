using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Xunit;
using RoslynCompilation = Microsoft.CodeAnalysis.Compilation;

namespace NetPrints.Analyzers.Tests.Infrastructure;

/// <summary>Runs an authoring (NPA) analyzer, and its code fix, over a node sample compiled against the real <c>NetPrints.Core</c>.</summary>
internal static class PinAnalyzerHarness
{
    private const string Authoring = "NetPrints.Authoring";

    private const string AuthoringCodeFixes = "NetPrints.Authoring.CodeFixes";

    private const int MaxFixRounds = 20;

    /// <summary>Asserts that the analyzer reports exactly the marked spans and nothing else with the given ids.</summary>
    /// <param name="markup">The source with <c>{|ID:text|}</c> markers.</param>
    /// <param name="ids">The ids to check; the markers must use these.</param>
    /// <returns>The diagnostics, for further assertions.</returns>
    public static async Task<ImmutableArray<Diagnostic>> AssertDiagnosticsAsync(string markup, params string[] ids)
    {
        (string source, IReadOnlyList<AnalyzerHarness.Expected> expected) = AnalyzerHarness.Parse(markup);
        ImmutableArray<Diagnostic> actual = await AnalyzeAsync(AuthoringHarness.Compile(source), ids);

        AnalyzerHarness.Expected[] reported = [.. actual.Select(diagnostic => new AnalyzerHarness.Expected(diagnostic.Id, diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length))
            .OrderBy(item => item.Start).ThenBy(item => item.Id, StringComparer.Ordinal)];

        Assert.Equal(expected, reported);
        return actual;
    }

    /// <summary>Applies the code fix for a diagnostic id again and again until the analyzer reports nothing fixable.</summary>
    /// <param name="source">The clean source text.</param>
    /// <param name="id">The diagnostic id to fix.</param>
    /// <returns>The fixed text.</returns>
    public static async Task<string> FixAsync(string source, string id)
    {
        CodeFixProvider provider = Fix(id);
        using AdhocWorkspace workspace = new();
        Project project = workspace.AddProject("Sample", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable))
            .AddMetadataReferences(AuthoringHarness.References);
        Document document = project.AddDocument("Sample.cs", SourceText.From(source));

        for (int round = 0; round < MaxFixRounds; round++)
        {
            RoslynCompilation compilation = await document.Project.GetCompilationAsync(TestContext.Current.CancellationToken)
                ?? throw new InvalidOperationException("The sample project has no compilation.");
            ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(compilation, [id]);
            if (diagnostics.IsEmpty)
            {
                break;
            }

            List<CodeAction> actions = [];
            await provider.RegisterCodeFixesAsync(new CodeFixContext(document, diagnostics[0], (action, _) => actions.Add(action), TestContext.Current.CancellationToken));
            if (actions.Count == 0)
            {
                break;
            }

            ImmutableArray<CodeActionOperation> operations = await actions[0].GetOperationsAsync(TestContext.Current.CancellationToken);
            ApplyChangesOperation apply = operations.OfType<ApplyChangesOperation>().Single();
            document = apply.ChangedSolution.GetDocument(document.Id) ?? throw new InvalidOperationException("The fixed document vanished.");
        }

        return (await document.GetTextAsync(TestContext.Current.CancellationToken)).ToString();
    }

    /// <summary>Finds the descriptor of an authoring diagnostic id.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <returns>The descriptor.</returns>
    public static DiagnosticDescriptor Descriptor(string id) =>
        Analyzers().SelectMany(analyzer => analyzer.SupportedDiagnostics).Single(descriptor => descriptor.Id == id);

    private static CodeFixProvider Fix(string id) =>
        AnalyzerBinaries.Load(AuthoringCodeFixes).GetTypes()
            .Where(type => !type.IsAbstract && typeof(CodeFixProvider).IsAssignableFrom(type))
            .Select(type => Assert.IsType<CodeFixProvider>(Activator.CreateInstance(type), exactMatch: false))
            .Single(provider => provider.FixableDiagnosticIds.Contains(id));

    private static IEnumerable<DiagnosticAnalyzer> Analyzers() =>
        AnalyzerBinaries.Load(Authoring).GetTypes()
            .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type) && type.GetCustomAttribute<DiagnosticAnalyzerAttribute>() is not null)
            .Select(type => Assert.IsType<DiagnosticAnalyzer>(Activator.CreateInstance(type), exactMatch: false));

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(RoslynCompilation compilation, string[] ids)
    {
        DiagnosticAnalyzer[] analyzers = [.. Analyzers().Where(analyzer => analyzer.SupportedDiagnostics.Any(descriptor => ids.Contains(descriptor.Id)))];
        ImmutableArray<Diagnostic> all = await compilation.WithAnalyzers([.. analyzers]).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        return [.. all.Where(diagnostic => ids.Contains(diagnostic.Id)).OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)];
    }
}
