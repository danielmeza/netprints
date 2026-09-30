using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Reflection;
using Xunit;

namespace NetPrints.Catalog.Tests.Emit;

/// <summary>D-R3: three catalogs of <c>System.Runtime</c> size compile together at C# 7.3, with no CS8103 (user string heap limit) and no C# 10 syntax.</summary>
public sealed class LargeCatalogEmitTests
{
    private const string TooManyUserStrings = "CS8103";

    private const string AttributeStub = """
        namespace NetPrints.Annotations
        {
            [System.AttributeUsage(System.AttributeTargets.Assembly, AllowMultiple = true)]
            internal sealed class NetPrintsEmbeddedCatalogAttribute : System.Attribute
            {
                public NetPrintsEmbeddedCatalogAttribute(string id, int schemaVersion, string json) { }
            }
        }
        """;

    private static IEnumerable<Diagnostic> Errors(IEnumerable<string> sources)
    {
        MetadataReference[] references =
        [
            .. FixtureCatalog.FrameworkReferences(),
            MetadataReference.CreateFromFile(typeof(CatalogLoader).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(ITypeCatalog).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(NetPrints.Core.TypeSpecifier).Assembly.Location),
        ];
        CSharpParseOptions parse = new(LanguageVersion.CSharp7_3);
        CSharpCompilation compilation = CSharpCompilation.Create(
            "Consumer",
            sources.Select(source => CSharpSyntaxTree.ParseText(source, parse)),
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        using System.IO.MemoryStream stream = new();
        return compilation.Emit(stream, cancellationToken: TestContext.Current.CancellationToken).Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ThreeSystemRuntimeCatalogsFromTheCSharpEmitterCompileTogetherAtCSharp7()
    {
        string[] sources = [.. Enumerable.Range(1, 3).Select(i => CatalogCSharpEmitter.Emit(SystemRuntimeCatalog.WithId($"big{i}"), $"Big{i}", "Catalog"))];

        Diagnostic[] errors = [.. Errors(sources)];

        Assert.DoesNotContain(errors, d => d.Id == TooManyUserStrings);
        Assert.Empty(errors);
    }

    [Fact]
    public void ThreeSystemRuntimeCatalogsFromTheEmbeddedEmitterCompileTogetherAtCSharp7()
    {
        string[] sources =
        [
            AttributeStub,
            .. Enumerable.Range(1, 3).Select(i => EmbeddedCatalogEmitter.Emit(SystemRuntimeCatalog.WithId($"big{i}"), $"Big{i}", "Runtime")),
        ];

        Diagnostic[] errors = [.. Errors(sources)];

        Assert.DoesNotContain(errors, d => d.Id == TooManyUserStrings);
        Assert.Empty(errors);
    }
}
