using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Xunit;

namespace NetPrints.Catalog.Tests.Generator;

/// <summary>AN-T01: the injected attributes work with no package reference, leave no NetPrints reference behind and never clash across InternalsVisibleTo.</summary>
public sealed class AttributeInjectionTests
{
    private const string Usage = """
        using NetPrints.Annotations;

        [assembly: NetPrintsCatalog("Some.Library", Id = "some", Profile = "public-api", Include = new[] { "A.*" }, Exclude = new[] { "A.B" }, AccessorName = "Some")]

        namespace Lib
        {
            [NetPrintsType(DisplayName = "Thing", Category = "Things")]
            public class Thing
            {
                [NetPrintsNode(DisplayName = "Do", Category = "Actions", Keywords = new[] { "a", "b" })]
                public void Do() { }

                [NetPrintsIgnore]
                public void Hidden() { }
            }
        }
        """;

    [Fact]
    public void AttributesAreAvailableWithoutAnyPackageReference()
    {
        var (output, generatorDiagnostics) = GeneratorTestHost.Run(GeneratorTestHost.Compile("Lib", Usage));

        Assert.Empty(generatorDiagnostics);
        Assert.Empty(GeneratorTestHost.Errors(output));
        Assert.Contains(output.SyntaxTrees, t => t.FilePath.EndsWith("NetPrintsAttributes.g.cs", System.StringComparison.Ordinal));
    }

    [Fact]
    public void TheCompiledAssemblyReferencesNoNetPrintsAssembly()
    {
        var (output, _) = GeneratorTestHost.Run(GeneratorTestHost.Compile("Lib", Usage));

        using PEReader pe = new(new System.IO.MemoryStream(GeneratorTestHost.Emit(output)));
        MetadataReader reader = pe.GetMetadataReader();
        string[] referenced = reader.AssemblyReferences
            .Select(handle => reader.GetString(reader.GetAssemblyReference(handle).Name))
            .ToArray();

        Assert.DoesNotContain(referenced, name => name.StartsWith("NetPrints", System.StringComparison.Ordinal));
        Assert.Contains(reader.TypeDefinitions, handle => reader.GetString(reader.GetTypeDefinition(handle).Name) == "NetPrintsTypeAttribute");
    }

    [Fact]
    public void TwoAssembliesWithInternalsVisibleToDoNotClash()
    {
        var (first, _) = GeneratorTestHost.Run(GeneratorTestHost.Compile(
            "First",
            Usage.Replace(
                "using NetPrints.Annotations;",
                "using NetPrints.Annotations;\nusing System.Runtime.CompilerServices;\n[assembly: InternalsVisibleTo(\"Second\")]",
                System.StringComparison.Ordinal)));

        var (second, _) = GeneratorTestHost.Run(GeneratorTestHost.Compile(
            "Second",
            Usage.Replace("namespace Lib", "namespace Other", System.StringComparison.Ordinal),
            first.ToMetadataReference()));

        Assert.Empty(GeneratorTestHost.Errors(first));
        Assert.Empty(GeneratorTestHost.Errors(second));
    }
}
