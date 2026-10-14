using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace NetPrints.Editor.Tests.Architecture;

/// <summary>ADR-0033: the ad-hoc debug output bans (RS0030) list every overload and fire on a sample of each.</summary>
public sealed class BannedSymbolsTests
{
    private const string RuleBannedApi = "RS0030";
    private const string AnalyzerPathMetadataKey = "BannedApiAnalyzersPath";
    private const string BannedSymbolsFile = "BannedSymbols.txt";
    private const string NLogLogManagerId = "T:NLog.LogManager";

    private static string RepositoryRoot()
    {
        string? directory = AppContext.BaseDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "NetPrints.slnx")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        return directory ?? throw new InvalidOperationException("NetPrints.slnx was not found above the test binary.");
    }

    private static string BannedText() => File.ReadAllText(Path.Combine(RepositoryRoot(), "src", BannedSymbolsFile));

    private static HashSet<string> BannedIds() =>
        [.. BannedText().Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith(';'))
            .Select(line => line.Split(';')[0])];

    private static CSharpCompilation Compile(string source)
    {
        string[] platform = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty).Split(Path.PathSeparator);
        return CSharpCompilation.Create(
            "Sample",
            [CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken)],
            platform.Where(path => path.Length > 0).Select(path => MetadataReference.CreateFromFile(path)),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    public static TheoryData<string, string> Families() => new()
    {
        { "System.IO.File", "AppendAll" },
        { "System.Diagnostics.Trace", "Write" },
        { "System.Diagnostics.Debug", "Write" },
    };

    [Theory]
    [MemberData(nameof(Families))]
    public void EveryPublicOverloadOfABannedFamilyHasItsOwnLine(string typeName, string prefix)
    {
        CSharpCompilation compilation = Compile("class C { }");
        INamedTypeSymbol type = Assert.IsAssignableFrom<INamedTypeSymbol>(compilation.GetTypeByMetadataName(typeName));
        HashSet<string> banned = BannedIds();

        string[] overloads =
        [.. type.GetMembers().OfType<IMethodSymbol>()
            .Where(method => method.DeclaredAccessibility == Accessibility.Public && method.Name.StartsWith(prefix, StringComparison.Ordinal))
            .Select(method => DocumentationCommentId.CreateDeclarationId(method) ?? string.Empty)];

        Assert.NotEmpty(overloads);
        string[] missing = [.. overloads.Where(id => !banned.Contains(id))];
        Assert.True(missing.Length == 0, "Missing from " + BannedSymbolsFile + ":\n" + string.Join('\n', missing));
    }

    [Fact]
    public void NLogLogManagerIsBannedAndNothingInSrcNamesIt()
    {
        Assert.Contains(NLogLogManagerId, BannedIds());

        string[] offenders =
        [.. Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => File.ReadAllText(path).Contains("LogManager", StringComparison.Ordinal))];
        Assert.Empty(offenders);
    }

    public static TheoryData<string, string> Samples() => new()
    {
        { "File.AppendAllText", "System.IO.File.AppendAllText(\"a\", \"b\");" },
        { "File.AppendAllText with encoding", "System.IO.File.AppendAllText(\"a\", \"b\", System.Text.Encoding.UTF8);" },
        { "File.AppendAllLines", "System.IO.File.AppendAllLines(\"a\", new[] { \"b\" });" },
        { "File.AppendAllBytes", "System.IO.File.AppendAllBytes(\"a\", new byte[] { 1 });" },
        { "File.AppendAllTextAsync", "System.IO.File.AppendAllTextAsync(\"a\", \"b\");" },
        { "File.AppendAllLinesAsync", "System.IO.File.AppendAllLinesAsync(\"a\", new[] { \"b\" });" },
        { "Trace.Write", "System.Diagnostics.Trace.Write(\"x\");" },
        { "Trace.WriteLine", "System.Diagnostics.Trace.WriteLine(\"x\");" },
        { "Trace.WriteIf", "System.Diagnostics.Trace.WriteIf(true, \"x\");" },
        { "Debug.Write", "System.Diagnostics.Debug.Write(\"x\");" },
        { "Debug.WriteLine", "System.Diagnostics.Debug.WriteLine(\"x\");" },
        { "Debug.WriteLineIf", "System.Diagnostics.Debug.WriteLineIf(true, \"x\");" },
        { "NLog.LogManager", "NLog.LogManager.GetCurrentClassLogger();" },
    };

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string statement)
    {
        string analyzerDirectory = typeof(BannedSymbolsTests).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => attribute.Key == AnalyzerPathMetadataKey).Value
            ?? throw new InvalidOperationException("Analyzer path metadata has no value.");
        string folder = Path.Combine(analyzerDirectory, "analyzers", "dotnet", "cs");
        ImmutableArray<DiagnosticAnalyzer> analyzers =
        [
            .. new[] { "Microsoft.CodeAnalysis.BannedApiAnalyzers.dll", "Microsoft.CodeAnalysis.CSharp.BannedApiAnalyzers.dll" }
                .Select(name => Assembly.LoadFrom(Path.Combine(folder, name)))
                .SelectMany(LoadableTypes)
                .Where(type => !type.IsAbstract && typeof(DiagnosticAnalyzer).IsAssignableFrom(type)
                    && type.GetCustomAttributes<DiagnosticAnalyzerAttribute>().Any(attribute => attribute.Languages.Contains(LanguageNames.CSharp)))
                .Select(type => (DiagnosticAnalyzer)(Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Cannot create {type}.")))
        ];

        CSharpCompilation compilation = Compile("class Sample { void Run() { " + statement + " } }");
        ImmutableArray<AdditionalText> files = [new InMemoryText(BannedSymbolsFile, BannedText())];
        return await compilation
            .WithAnalyzers(analyzers, new AnalyzerOptions(files))
            .GetAnalyzerDiagnosticsAsync(TestContext.Current.CancellationToken);
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public async Task ASampleOfEachBannedApiReportsRs0030(string name, string statement)
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync(statement);

        Assert.True(diagnostics.Any(diagnostic => diagnostic.Id == RuleBannedApi), $"{name} did not report {RuleBannedApi}.");
    }

    [Fact]
    public async Task ASampleOfAnAllowedApiReportsNothing()
    {
        ImmutableArray<Diagnostic> diagnostics = await AnalyzeAsync("System.IO.File.ReadAllText(\"a\");");

        Assert.DoesNotContain(diagnostics, diagnostic => diagnostic.Id == RuleBannedApi);
    }

    private static Type[] LoadableTypes(Assembly assembly)
    {
        try
        {
            return assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            return [.. exception.Types.OfType<Type>()];
        }
    }

    private sealed class InMemoryText(string path, string text) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
