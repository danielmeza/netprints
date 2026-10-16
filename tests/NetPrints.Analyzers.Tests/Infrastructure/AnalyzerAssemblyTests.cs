using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace NetPrints.Analyzers.Tests.Infrastructure;

/// <summary>
/// ADR-0024: the four analyzer assemblies load, every analyzer in them declares its supported diagnostics, and every
/// code fix targets a diagnostic its sibling analyzer assembly declares.
/// </summary>
public class AnalyzerAssemblyTests
{
    private const string AuthoringAssembly = "NetPrints.Authoring";

    private const string RepoAnalyzersAssembly = "NetPrints.RepoAnalyzers";

    private const string CodeFixesSuffix = ".CodeFixes";

    private const string AnalyzerFault = "AD0001";

    public static TheoryData<string> AnalyzerAssemblies => [AuthoringAssembly, RepoAnalyzersAssembly];

    public static TheoryData<string> AllAssemblies =>
        [AuthoringAssembly, AuthoringAssembly + CodeFixesSuffix, RepoAnalyzersAssembly, RepoAnalyzersAssembly + CodeFixesSuffix];

    [Theory]
    [MemberData(nameof(AllAssemblies))]
    public void EveryAnalyzerAssemblyLoadsItsTypes(string name)
    {
        Assert.All(Load(name).GetTypes(), type => Assert.NotNull(type.FullName));
    }

    [Theory]
    [MemberData(nameof(AnalyzerAssemblies))]
    public void EveryAnalyzerDeclaresSupportedDiagnostics(string name)
    {
        foreach (DiagnosticAnalyzer analyzer in Analyzers(Load(name)))
        {
            Assert.NotEmpty(analyzer.SupportedDiagnostics);
            Assert.All(analyzer.SupportedDiagnostics, descriptor => Assert.Contains(descriptor.Id, ReleaseTracked(name), StringComparer.Ordinal));
        }
    }

    [Theory]
    [MemberData(nameof(AnalyzerAssemblies))]
    public async Task AnalyzersRunInACompilationWithAnalyzersHost(string name)
    {
        ImmutableArray<DiagnosticAnalyzer> analyzers = [.. Analyzers(Load(name))];
        if (analyzers.IsEmpty)
        {
            return;
        }

        CSharpCompilation compilation = CSharpCompilation.Create(
            "AnalyzerHostSample",
            [CSharpSyntaxTree.ParseText("namespace Sample { public class Empty { } }", cancellationToken: TestContext.Current.CancellationToken)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);

        ImmutableArray<Diagnostic> diagnostics = await compilation.WithAnalyzers(analyzers).GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == AnalyzerFault);
    }

    [Theory]
    [InlineData(AuthoringAssembly)]
    [InlineData(RepoAnalyzersAssembly)]
    public void EveryCodeFixTargetsADiagnosticOfItsAnalyzerAssembly(string name)
    {
        HashSet<string> declared = [.. Analyzers(Load(name)).SelectMany(analyzer => analyzer.SupportedDiagnostics).Select(descriptor => descriptor.Id)];
        IEnumerable<CodeFixProvider> providers = Load(name + CodeFixesSuffix).GetTypes()
            .Where(type => !type.IsAbstract && typeof(CodeFixProvider).IsAssignableFrom(type))
            .Select(type => Assert.IsAssignableFrom<CodeFixProvider>(Activator.CreateInstance(type)));

        foreach (CodeFixProvider provider in providers)
        {
            Assert.NotEmpty(provider.FixableDiagnosticIds);
            Assert.All(provider.FixableDiagnosticIds, id => Assert.Contains(id, declared));
        }
    }

    private static IEnumerable<DiagnosticAnalyzer> Analyzers(Assembly assembly) =>
        assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type) && type.GetCustomAttribute<DiagnosticAnalyzerAttribute>() is not null)
            .Select(type => Assert.IsAssignableFrom<DiagnosticAnalyzer>(Activator.CreateInstance(type)));

    private static IReadOnlyList<string> ReleaseTracked(string name) =>
    [
        .. Directory.EnumerateFiles(Path.Combine(Metadata("SourceRoot"), name), "AnalyzerReleases.*.md")
            .SelectMany(File.ReadAllLines)
            .Where(line => line.Length > 0 && line[0] != ';' && line[0] != '#')
            .Select(line => line.Split('|')[0].Trim())
            .Where(id => id.Length > 0 && !id.StartsWith('-')),
    ];

    private static Assembly Load(string name) =>
        Assembly.LoadFrom(Path.Combine(Metadata("SourceRoot"), name, "bin", Metadata("Configuration"), "netstandard2.0", name + ".dll"));

    private static string Metadata(string key) =>
        typeof(AnalyzerAssemblyTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().Single(attribute => attribute.Key == key).Value
            ?? throw new InvalidOperationException($"Assembly metadata {key} is missing.");
}
