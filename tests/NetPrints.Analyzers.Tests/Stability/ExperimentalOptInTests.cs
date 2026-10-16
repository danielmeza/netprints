using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Analyzers.Tests.Infrastructure;
using Xunit;

namespace NetPrints.Analyzers.Tests.Stability;

/// <summary>FR-030, US3 scenario 5, ADR-0017: an extension that uses an experimental API without opting in to its id does not compile, and the id says which one.</summary>
public class ExperimentalOptInTests
{
    private const string Header = "using System.Resources;\nnamespace Fx;\n";

    private static readonly ImmutableArray<MetadataReference> References =
    [
        .. GeneratorHarness.References,
        .. Directory.GetFiles(AppContext.BaseDirectory, "NetPrints.*.dll")
            .Where(path => !Path.GetFileName(path).Contains("Analyzers", StringComparison.Ordinal) && !Path.GetFileName(path).Contains("Authoring", StringComparison.Ordinal) && !Path.GetFileName(path).Contains("Testing", StringComparison.Ordinal))
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)),
    ];

    public static TheoryData<string, string> Surfaces() => new()
    {
        { "NPXE0003", "public sealed class Fx { public NetPrints.Translator.IClassEmitter? Emitter; }" },
        { "NPXE0005", "public sealed class Fx { public NetPrints.Extensibility.Editor.IProjectSession? Session; }" },
        { "NPXE0006", "public sealed class Fx { public NetPrints.Extensibility.Nodes.INodeSuggestionProvider? Provider; }" },
        { "NPXE0007", "public sealed class Fx { public NetPrints.Extensibility.Nodes.CSharpFeature? Feature; }" },
        { "NPXE0011", "public sealed class Fx { public void Add(NetPrints.Extensibility.IExtensionBuilder builder, ResourceManager resources) => builder.AddStringResources(resources); }" },
    };

    private static IReadOnlyList<Diagnostic> Errors(string source, params string[] optedIn)
    {
        var options = new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable)
            .WithSpecificDiagnosticOptions(optedIn.ToDictionary(id => id, _ => ReportDiagnostic.Suppress, StringComparer.Ordinal));
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Fx",
            [CSharpSyntaxTree.ParseText(Header + source, cancellationToken: TestContext.Current.CancellationToken)],
            References,
            options);
        return [.. compilation.GetDiagnostics(TestContext.Current.CancellationToken).Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void UsingAnExperimentalApiWithoutTheOptInFailsWithItsId(string id, string source)
    {
        IReadOnlyList<Diagnostic> errors = Errors(source);

        Assert.Contains(errors, error => error.Id == id);
    }

    [Theory]
    [MemberData(nameof(Surfaces))]
    public void TheOptInOfTheIdAloneMakesItCompile(string id, string source)
    {
        Assert.Empty(Errors(source, id));
    }

    [Fact]
    public void TheOptInOfAnotherIdDoesNotHelp()
    {
        IReadOnlyList<Diagnostic> errors = Errors("public sealed class Fx { public NetPrints.Extensibility.Nodes.CSharpFeature? Feature; }", "NPXE0006");

        Assert.Contains(errors, error => error.Id == "NPXE0007");
    }
}
