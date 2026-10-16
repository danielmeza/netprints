using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
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

/// <summary>Runs a repository analyzer, and its code fix, over a source text with the diagnostics marked in it.</summary>
internal static class AnalyzerHarness
{
    private const string RepoAnalyzers = "NetPrints.RepoAnalyzers";

    private const string RepoCodeFixes = "NetPrints.RepoAnalyzers.CodeFixes";

    private const string OpenMarker = "{|";

    private const string CloseMarker = "|}";

    private const int MaxFixRounds = 200;

    /// <summary>A diagnostic expected at an exact span.</summary>
    /// <param name="Id">The diagnostic id.</param>
    /// <param name="Start">The span start.</param>
    /// <param name="Length">The span length.</param>
    public sealed record Expected(string Id, int Start, int Length);

    /// <summary>Strips the <c>{|ID:text|}</c> markers of a source and returns the clean source with the spans they marked.</summary>
    /// <param name="markup">The source with markers.</param>
    /// <returns>The clean source and the expected diagnostics in source order.</returns>
    public static (string Source, IReadOnlyList<Expected> Expected) Parse(string markup)
    {
        StringBuilder source = new();
        List<Expected> expected = [];
        Stack<(string Id, int Start)> open = new();
        int index = 0;

        while (index < markup.Length)
        {
            if (string.CompareOrdinal(markup, index, OpenMarker, 0, OpenMarker.Length) == 0)
            {
                int colon = markup.IndexOf(':', index);
                open.Push((markup.Substring(index + OpenMarker.Length, colon - index - OpenMarker.Length), source.Length));
                index = colon + 1;
            }
            else if (string.CompareOrdinal(markup, index, CloseMarker, 0, CloseMarker.Length) == 0)
            {
                (string id, int start) = open.Pop();
                expected.Add(new Expected(id, start, source.Length - start));
                index += CloseMarker.Length;
            }
            else
            {
                source.Append(markup[index]);
                index++;
            }
        }

        return (source.ToString(), [.. expected.OrderBy(item => item.Start).ThenBy(item => item.Id, StringComparer.Ordinal)]);
    }

    /// <summary>Runs the repository analyzer that supports a diagnostic id.</summary>
    /// <param name="source">The clean source text.</param>
    /// <param name="id">The diagnostic id to keep.</param>
    /// <returns>The diagnostics with that id, in source order.</returns>
    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string source, string id)
    {
        CSharpCompilation compilation = Compile(source);
        return await RunAsync(compilation, Analyzer(id), id);
    }

    /// <summary>Asserts that the analyzer reports exactly the marked spans and nothing else with the given ids.</summary>
    /// <param name="markup">The source with <c>{|ID:text|}</c> markers.</param>
    /// <param name="ids">The ids to check; the markers must use these.</param>
    /// <returns>The diagnostics, for further assertions.</returns>
    public static async Task<ImmutableArray<Diagnostic>> AssertDiagnosticsAsync(string markup, params string[] ids)
    {
        (string source, IReadOnlyList<Expected> expected) = Parse(markup);
        List<Diagnostic> actual = [];
        foreach (string id in ids)
        {
            actual.AddRange(await AnalyzeAsync(source, id));
        }

        Expected[] reported = [.. actual.Select(diagnostic => new Expected(diagnostic.Id, diagnostic.Location.SourceSpan.Start, diagnostic.Location.SourceSpan.Length))
            .OrderBy(item => item.Start).ThenBy(item => item.Id, StringComparer.Ordinal)];

        Assert.Equal(expected, reported);
        return [.. actual];
    }

    /// <summary>Applies the code fix for a diagnostic id again and again until the analyzer reports nothing fixable.</summary>
    /// <param name="source">The clean source text.</param>
    /// <param name="id">The diagnostic id to fix.</param>
    /// <returns>The fixed text.</returns>
    public static async Task<string> FixAsync(string source, string id)
    {
        DiagnosticAnalyzer analyzer = Analyzer(id);
        CodeFixProvider provider = Fix(id);
        using AdhocWorkspace workspace = new();
        Project project = workspace.AddProject("Sample", LanguageNames.CSharp).AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        Document document = project.AddDocument("Sample.cs", SourceText.From(source));

        for (int round = 0; round < MaxFixRounds; round++)
        {
            RoslynCompilation compilation = await document.Project.GetCompilationAsync(TestContext.Current.CancellationToken)
                ?? throw new InvalidOperationException("The sample project has no compilation.");
            ImmutableArray<Diagnostic> diagnostics = await RunAsync(compilation, analyzer, id);
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

    /// <summary>Finds the code fix provider that fixes a diagnostic id.</summary>
    /// <param name="id">The diagnostic id.</param>
    /// <returns>The provider.</returns>
    public static CodeFixProvider Fix(string id) =>
        AnalyzerBinaries.Load(RepoCodeFixes).GetTypes()
            .Where(type => !type.IsAbstract && typeof(CodeFixProvider).IsAssignableFrom(type))
            .Select(type => Assert.IsType<CodeFixProvider>(Activator.CreateInstance(type), exactMatch: false))
            .Single(provider => provider.FixableDiagnosticIds.Contains(id));

    private static DiagnosticAnalyzer Analyzer(string id) =>
        AnalyzerBinaries.Load(RepoAnalyzers).GetTypes()
            .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type) && type.GetCustomAttribute<DiagnosticAnalyzerAttribute>() is not null)
            .Select(type => Assert.IsType<DiagnosticAnalyzer>(Activator.CreateInstance(type), exactMatch: false))
            .Single(analyzer => analyzer.SupportedDiagnostics.Any(descriptor => descriptor.Id == id));

    private static CSharpCompilation Compile(string source) =>
        CSharpCompilation.Create(
            "Sample",
            [CSharpSyntaxTree.ParseText(source, path: "Sample.cs", cancellationToken: TestContext.Current.CancellationToken)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

    private static async Task<ImmutableArray<Diagnostic>> RunAsync(RoslynCompilation compilation, DiagnosticAnalyzer analyzer, string id)
    {
        DiagnosticDescriptor descriptor = analyzer.SupportedDiagnostics.Single(candidate => candidate.Id == id);
        ReportDiagnostic report = descriptor.DefaultSeverity == DiagnosticSeverity.Error ? ReportDiagnostic.Error : ReportDiagnostic.Info;
        compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions([new KeyValuePair<string, ReportDiagnostic>(id, report)]));
        ImmutableArray<Diagnostic> all = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
        return [.. all.Where(diagnostic => diagnostic.Id == id).OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start)];
    }
}
