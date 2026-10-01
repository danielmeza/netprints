using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace NetPrints.Editor.Tests.Architecture;

/// <summary>
/// contracts/editor-services.md §7: a Roslyn scan of every editor view model file for rules A1
/// (no Avalonia/Nodify types) and A2 (no direct reference to the owning class/main editor), plus the
/// fixture that proves the scanner actually fires (ED-T10). Rule A3 (assembly references) is a
/// separate, non-Roslyn check in <see cref="AssemblyReferenceGateTests"/>.
/// </summary>
public class ArchitectureGateTests
{
    private const string ClassEditorVMTypeName = "ClassEditorViewModel";
    private const string MainEditorVMTypeName = "MainEditorViewModel";
    private const string RuleA1 = "A1";
    private const string RuleA2 = "A2";

    /// <summary>One rule violation, keyed by the file it fired in: at most one record per (rule, file), not per occurrence.</summary>
    private readonly record struct Violation(string Rule, string File);

    private static string[] EditorSourceFiles(string editorSrc) =>
        [.. Directory.EnumerateFiles(editorSrc, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))];

    private static bool IsViewModelFile(string path) =>
        path.EndsWith("VM.cs", StringComparison.Ordinal) || Path.GetFileName(path).Contains("ViewModel", StringComparison.Ordinal);

    /// <summary>Compiles <paramref name="files"/> against every assembly next to the test binary, so the
    /// semantic model resolves the editor's own Avalonia/Nodify/CommunityToolkit references.</summary>
    private static CSharpCompilation BuildCompilation(IEnumerable<(string Path, string Text)> files)
    {
        var trees = files.Select(f => CSharpSyntaxTree.ParseText(f.Text, path: f.Path)).ToList();
        var references = Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll")
            .Select(TryCreateReference)
            .OfType<MetadataReference>();

        return CSharpCompilation.Create("ArchitectureGate", trees, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static MetadataReference? TryCreateReference(string path)
    {
        try
        {
            return MetadataReference.CreateFromFile(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (BadImageFormatException)
        {
            return null;
        }
    }

    private static string? NamespaceOf(ISymbol? symbol) => symbol switch
    {
        INamespaceSymbol ns => ns.ToDisplayString(),
        ITypeSymbol type => type.ContainingNamespace?.ToDisplayString(),
        IMethodSymbol method => method.ContainingType?.ContainingNamespace?.ToDisplayString(),
        IPropertySymbol property => property.ContainingType?.ContainingNamespace?.ToDisplayString(),
        IFieldSymbol field => field.ContainingType?.ContainingNamespace?.ToDisplayString(),
        IEventSymbol ev => ev.ContainingType?.ContainingNamespace?.ToDisplayString(),
        _ => null,
    };

    private static bool IsBannedNamespace(string? ns) =>
        ns is not null && (ns.StartsWith("Avalonia", StringComparison.Ordinal) || ns.StartsWith("Nodify", StringComparison.Ordinal));

    private static bool IsBannedOwnerType(SemanticModel model, TypeSyntax? type) =>
        type is not null && model.GetSymbolInfo(type).Symbol is INamedTypeSymbol { Name: ClassEditorVMTypeName or MainEditorVMTypeName };

    /// <summary>Scans one view model syntax tree for A1 and A2 (editor-services.md §7).</summary>
    private static HashSet<Violation> Scan(SemanticModel model, SyntaxTree tree)
    {
        var violations = new HashSet<Violation>();
        string file = Path.GetFileName(tree.FilePath);
        SyntaxNode root = tree.GetRoot();

        bool usesBannedNamespace = root.DescendantNodes().OfType<SimpleNameSyntax>()
            .Select(name => model.GetSymbolInfo(name) is { Symbol: { } symbol } ? symbol : model.GetSymbolInfo(name).CandidateSymbols.FirstOrDefault())
            .Any(symbol => IsBannedNamespace(NamespaceOf(symbol)));
        if (usesBannedNamespace)
        {
            violations.Add(new Violation(RuleA1, file));
        }

        bool isClassOrMainEditorVM = file is "ClassEditorViewModel.cs" or "MainEditorViewModel.cs";
        bool holdsTheOwningEditor = !isClassOrMainEditorVM &&
            (root.DescendantNodes().OfType<ConstructorDeclarationSyntax>().SelectMany(c => c.ParameterList.Parameters)
                .Any(p => IsBannedOwnerType(model, p.Type))
            || root.DescendantNodes().OfType<FieldDeclarationSyntax>()
                .Any(f => IsBannedOwnerType(model, f.Declaration.Type)));
        if (holdsTheOwningEditor)
        {
            violations.Add(new Violation(RuleA2, file));
        }

        return violations;
    }

    [Fact]
    public void RealViewModelsHaveNoViolations()
    {
        string editorSrc = Path.Combine(RepositoryPaths.Root(), "src", "NetPrints.Editor");
        var files = EditorSourceFiles(editorSrc).Select(path => (Path: path, Text: File.ReadAllText(path))).ToList();
        CSharpCompilation compilation = BuildCompilation(files);

        var vmTrees = compilation.SyntaxTrees.Where(t => IsViewModelFile(t.FilePath)).ToList();
        Assert.NotEmpty(vmTrees); // ED-T10: a non-empty set of scanned files.

        var violations = vmTrees.SelectMany(tree => Scan(compilation.GetSemanticModel(tree), tree)).ToList();
        Assert.Empty(violations.Select(v => $"{v.Rule}: {v.File}"));
    }

    [Fact]
    public void FixtureFailsOnExactlyItsTwoViolations()
    {
        string editorSrc = Path.Combine(RepositoryPaths.Root(), "src", "NetPrints.Editor");
        string fixturePath = Path.Combine(RepositoryPaths.Root(), "tests", "NetPrints.Editor.Tests", "Architecture", "Fixtures", "ViolatingViewModel.cs.txt");
        string fixtureSourcePath = Path.Combine(editorSrc, "ViolatingViewModel.cs"); // A2's exemption is by file name only; this name is not exempt.

        var files = EditorSourceFiles(editorSrc).Select(path => (Path: path, Text: File.ReadAllText(path)))
            .Append((Path: fixtureSourcePath, Text: File.ReadAllText(fixturePath)))
            .ToList();
        CSharpCompilation compilation = BuildCompilation(files);

        SyntaxTree fixtureTree = compilation.SyntaxTrees.Single(t => t.FilePath == fixtureSourcePath);
        HashSet<Violation> violations = Scan(compilation.GetSemanticModel(fixtureTree), fixtureTree);

        Assert.Equal([new Violation(RuleA1, "ViolatingViewModel.cs"), new Violation(RuleA2, "ViolatingViewModel.cs")], violations.OrderBy(v => v.Rule, StringComparer.Ordinal));
    }
}
