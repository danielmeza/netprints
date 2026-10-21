using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Extensibility.Nodes;

namespace NetPrints.Extensibility.Testing.Checks;

/// <summary>NPK007: the C# generated for a class with a node of each kind of the extension compiles.</summary>
internal sealed class TranslationCompilesCheck : ConformanceCheck
{
    private const string Rule = "the generated C# compiles";
    private const string AssemblyName = "ConformanceCompile";
    private const int MaxErrorsPerKind = 3;

    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(LoadReferences);

    public override string Code => NpkDiagnosticCodes.TranslationCompiles;

    public override string Name => "Translation compiles";

    public override Task<CheckOutcome> RunAsync(CheckContext context, CancellationToken cancellationToken)
    {
        IReadOnlyList<NodeKindDescriptor> kinds = context.SubjectKinds;
        IReadOnlyList<NodeKindDescriptor> unexercised = context.UnexercisedKinds;
        if (kinds.Count == 0)
        {
            return Task.FromResult(unexercised.Count > 0
                ? CheckOutcome.Skip(CheckContext.NotExercised(unexercised))
                : CheckOutcome.Passed("the extension declares no node kind"));
        }

        var findings = new List<ConformanceFinding>();
        foreach (NodeKindDescriptor kind in kinds)
        {
            try
            {
                string code = context.Harness.Translate(context.Harness.BuildClass(ConformanceGraphs.Namespace, ConformanceGraphs.ClassName, kind.Kind));
                CSharpCompilation compilation = CSharpCompilation.Create(
                    AssemblyName,
                    [CSharpSyntaxTree.ParseText(code, cancellationToken: cancellationToken)],
                    References.Value,
                    new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
                foreach (Diagnostic diagnostic in compilation.GetDiagnostics(cancellationToken).Where(d => d.Severity == DiagnosticSeverity.Error).Take(MaxErrorsPerKind))
                {
                    findings.Add(Finding(Rule, $"{kind.Kind}: {diagnostic.Id} {diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture)}"));
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                findings.Add(Finding(Rule, $"{kind.Kind}: translation threw {ex.GetType().Name}: {ex.Message}"));
            }
        }

        return Task.FromResult(findings.Count == 0 && unexercised.Count > 0
            ? CheckOutcome.Skip(CheckContext.NotExercised(unexercised))
            : CheckOutcome.FromFindings(findings));
    }

    private static List<MetadataReference> LoadReferences()
    {
        var references = new List<MetadataReference>();
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string list)
        {
            foreach (string path in list.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        return references;
    }
}
