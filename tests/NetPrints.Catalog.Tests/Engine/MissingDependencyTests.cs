using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace NetPrints.Catalog.Tests.Engine;

/// <summary>CT-T19 (NPC005): members that use a type from an assembly that is not available are omitted with a warning; the rest is produced.</summary>
public sealed class MissingDependencyTests
{
    private const string DependencySource = "namespace Dep { public class Widget { } public interface IMarker { } public class BaseThing { } }";

    private const string DependentSource = """
        namespace Lib
        {
            public class Holder
            {
                public int Good(int x) => x;
                public void TakesWidget(Dep.Widget widget) { }
                public Dep.Widget ReturnsWidget() => default;
                public System.Collections.Generic.List<Dep.Widget> ReturnsList() => default;
                public Dep.Widget Field;
                public Dep.Widget Property { get; set; }
                public Holder(Dep.Widget widget) { }
                public Holder() { }
            }

            public class DerivedFromMissing : Dep.BaseThing { }

            public class Plain { public string Name { get; set; } }
        }
        """;

    private static (CatalogBuildResult Result, CSharpCompilation Compilation) BuildWithoutDependency()
    {
        CSharpCompilation dependency = FixtureCatalog.CreateCompilation("Dep", FixtureCatalog.FrameworkReferences(), CSharpSyntaxTree.ParseText(DependencySource, cancellationToken: TestContext.Current.CancellationToken));
        using MemoryStream dependencyStream = new();
        Assert.True(dependency.Emit(dependencyStream, cancellationToken: TestContext.Current.CancellationToken).Success);

        CSharpCompilation dependent = FixtureCatalog.CreateCompilation(
            "Lib",
            [.. FixtureCatalog.FrameworkReferences(), MetadataReference.CreateFromStream(new MemoryStream(dependencyStream.ToArray()))],
            CSharpSyntaxTree.ParseText(DependentSource, cancellationToken: TestContext.Current.CancellationToken));
        using MemoryStream dependentStream = new();
        Assert.True(dependent.Emit(dependentStream, cancellationToken: TestContext.Current.CancellationToken).Success);

        MetadataReference lib = MetadataReference.CreateFromStream(new MemoryStream(dependentStream.ToArray()));
        CSharpCompilation tool = FixtureCatalog.CreateCompilation("Tool", [.. FixtureCatalog.FrameworkReferences(), lib]);
        IAssemblySymbol assembly = FixtureCatalog.AssemblyOf(tool, lib);
        CatalogBuildResult result = CatalogBuilder.Build(tool, [assembly], new CatalogProfileFilter(BuiltInCatalogProfiles.PublicApi), XmlDocumentationSource.Empty, new CatalogIdentity());
        return (result, tool);
    }

    [Fact]
    public void OmitsMembersUsingTheMissingAssemblyAndKeepsTheRest()
    {
        (CatalogBuildResult result, _) = BuildWithoutDependency();

        CatalogType holder = FixtureCatalog.TypeOf(result, "T:Lib.Holder");
        Assert.Equal(["Good"], holder.Methods?.Select(m => m.Name));
        Assert.Equal("M:Lib.Holder.#ctor", Assert.Single(holder.Constructors ?? []).Id);
        Assert.Null(holder.Variables);
        Assert.Equal("Name", FixtureCatalog.TypeOf(result, "T:Lib.Plain").Variables?.Single().Name);
    }

    [Fact]
    public void ReportsOneWarningPerOmittedSymbolNamingTheMissingAssembly()
    {
        (CatalogBuildResult result, _) = BuildWithoutDependency();

        Assert.All(result.Diagnostics, d =>
        {
            Assert.Equal(CatalogDiagnosticCodes.SkippedMemberMissingAssembly, d.Code);
            Assert.Equal(CatalogDiagnosticSeverity.Warning, d.Severity);
            Assert.Contains("Dep", d.Message, System.StringComparison.Ordinal);
        });
        string?[] sources = [.. result.Diagnostics.Select(d => d.Source)];
        Assert.Contains("M:Lib.Holder.TakesWidget(Dep.Widget)", sources);
        Assert.Contains("M:Lib.Holder.ReturnsWidget", sources);
        Assert.Contains("M:Lib.Holder.ReturnsList", sources);
        Assert.Contains("F:Lib.Holder.Field", sources);
        Assert.Contains("P:Lib.Holder.Property", sources);
        Assert.Contains("M:Lib.Holder.#ctor(Dep.Widget)", sources);
    }

    [Fact]
    public void OmitsATypeWhoseBaseTypeIsMissing()
    {
        (CatalogBuildResult result, _) = BuildWithoutDependency();

        Assert.DoesNotContain(result.Document.Types, type => type.Id == "T:Lib.DerivedFromMissing");
        Assert.Contains(result.Diagnostics, d => d.Source == "T:Lib.DerivedFromMissing");
    }
}
