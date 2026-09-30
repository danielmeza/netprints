using System.Linq;
using Xunit;

namespace NetPrints.Catalog.Tests.Engine;

/// <summary>CT-T04: the built-in <c>public-api</c> profile over the fixture, covering every edge case of the spec.</summary>
public sealed class PublicApiProfileTests
{
    private static readonly CatalogBuildResult Result = FixtureCatalog.BuildFixture(BuiltInCatalogProfiles.PublicApi, FixtureCatalog.FixtureId);

    private static CatalogMethod Method(string typeId, string name) =>
        FixtureCatalog.TypeOf(Result, typeId).Methods?.Single(method => method.Name == name)
        ?? throw new Xunit.Sdk.XunitException($"{typeId} has no methods.");

    [Fact]
    public void EqualsTheCommittedSnapshot() => FixtureCatalog.AssertSnapshot("public-api.npcat.json", Result);

    [Fact]
    public void ProducesNoDiagnosticsAndTheDocumentIdentity()
    {
        FixtureCatalog.AssertNoDiagnostics(Result);
        Assert.Equal(FixtureCatalog.FixtureId, Result.Document.Id);
        Assert.Equal("1.0.0.0", Result.Document.Version);
        Assert.Equal("public-api", Result.Document.Profile);
        Assert.Equal("CatalogFixtureLib", Assert.Single(Result.Document.Assemblies).Name);
    }

    [Fact]
    public void ListsTypesSortedByIdAndOnlyPublicOnes()
    {
        string[] ids = [.. Result.Document.Types.Select(type => type.Id)];

        Assert.Equal([.. ids.OrderBy(id => id, System.StringComparer.Ordinal)], ids);
        Assert.DoesNotContain(ids, id => id.Contains("Embedded", System.StringComparison.Ordinal) || id.Contains("NetPrints.Annotations", System.StringComparison.Ordinal));
    }

    [Fact]
    public void GenericTypesMethodsAndNestedTypes()
    {
        CatalogType box = FixtureCatalog.TypeOf(Result, "T:Fixture.Generics.Box`1");
        Assert.Equal(["T"], box.GenericParameters);
        CatalogMethod map = Method("T:Fixture.Generics.Box`1", "Map");
        Assert.Equal(["TResult"], map.GenericParameters);
        Assert.Equal("System.Func", map.Parameters?.Single().Type.Name);
        Assert.Equal(["T", "TResult"], map.Parameters?.Single().Type.Args?.Select(a => a.Name));
        Assert.All(map.Parameters?.Single().Type.Args ?? [], a => Assert.True(a.Generic));

        CatalogType handle = FixtureCatalog.TypeOf(Result, "T:Fixture.Generics.Box`1.Handle");
        Assert.Equal("T:Fixture.Generics.Box`1", handle.DeclaringType);
        Assert.Equal("Handle", handle.Name);
        Assert.Equal("Fixture.Generics", handle.Namespace);
    }

    [Fact]
    public void ParameterModifiersDefaultsAndParamsArrays()
    {
        const string Helpers = "T:Fixture.Utilities.Helpers";
        Assert.Equal(CatalogPassType.Out, Method(Helpers, "TryParse").Parameters?[1].PassType);
        Assert.Equal(CatalogPassType.Reference, Method(Helpers, "Swap").Parameters?[0].PassType);
        Assert.Equal(CatalogPassType.In, Method(Helpers, "LengthOf").Parameters?[0].PassType);
        Assert.True(Method(Helpers, "Sum").Parameters?[0].Params);

        CatalogParameter[] format = [.. Method(Helpers, "Format").Parameters ?? []];
        Assert.Null(format[0].Default);
        Assert.Equal(new CatalogTypedValue { Type = "System.Int32", Value = "10" }, format[1].Default);
        Assert.Equal(new CatalogTypedValue { Type = "System.Char", Value = " " }, format[2].Default);
        Assert.Equal(new CatalogTypedValue { Type = "System.String" }, format[3].Default);
        Assert.Equal(new CatalogTypedValue { Type = "System.Int32", Value = "1" }, format[4].Default);
        Assert.Equal(new CatalogTypedValue { Type = "System.Double", Value = "0.5" }, format[5].Default);
    }

    [Fact]
    public void ExtensionMethodsAndOperators()
    {
        Assert.Contains("extension", Method("T:Fixture.Utilities.Helpers", "Reverse").Modifiers ?? []);
        Assert.Equal(["static", "extension"], Method("T:Fixture.Utilities.Helpers", "FirstOrNothing").Modifiers);

        CatalogMethod[] implicits = [.. FixtureCatalog.TypeOf(Result, "T:Fixture.Geometry.Vector2").Methods?.Where(m => m.Name == "op_Implicit") ?? []];
        CatalogMethod implicitConversion = Assert.Single(implicits);
        Assert.Equal(["static", "operator"], implicitConversion.Modifiers);
        Assert.Equal("System.Single", implicitConversion.Parameters?.Single().Type.Name);
        Assert.Contains(FixtureCatalog.TypeOf(Result, "T:Fixture.Geometry.Vector2").Methods ?? [], m => m.Name == "op_Explicit");
        Assert.Contains(FixtureCatalog.TypeOf(Result, "T:Fixture.Geometry.Vector2").Methods ?? [], m => m.Name == "op_Addition");
    }

    [Fact]
    public void ObsoleteMembersFollowTheDefaultExcludeErrorsRule()
    {
        CatalogType old = FixtureCatalog.TypeOf(Result, "T:Fixture.Legacy.Old");
        Assert.Equal("Use NewName.", old.Methods?.Single(m => m.Name == "OldName").Obsolete?.Message);
        Assert.DoesNotContain(old.Methods ?? [], m => m.Name == "Removed");
        Assert.Contains(Result.Document.Types, type => type.Id == "T:Fixture.Legacy.Ancient");
        Assert.Contains("Big", FixtureCatalog.TypeOf(Result, "T:Fixture.Legacy.Level").EnumMembers ?? []);
        Assert.Equal(["Low", "High", "Big"], FixtureCatalog.TypeOf(Result, "T:Fixture.Legacy.Level").EnumMembers);
    }

    [Fact]
    public void InheritanceProtectedMembersAndInterfaces()
    {
        CatalogType shape = FixtureCatalog.TypeOf(Result, "T:Fixture.Geometry.ShapeBase");
        Assert.Contains("abstract", shape.Modifiers ?? []);
        Assert.Equal(CatalogVisibility.Protected, Assert.Single(shape.Constructors ?? []).Visibility);
        Assert.Equal("Fixture.Geometry.IShape", Assert.Single(shape.Interfaces ?? []).Name);
        Assert.Equal(["abstract"], Method("T:Fixture.Geometry.ShapeBase", "Area").Modifiers);
        Assert.Equal(["virtual"], Method("T:Fixture.Geometry.ShapeBase", "Describe").Modifiers);

        CatalogType circle = FixtureCatalog.TypeOf(Result, "T:Fixture.Geometry.Circle");
        Assert.Equal("Fixture.Geometry.ShapeBase", circle.BaseType?.Name);
        Assert.Equal(["override"], Method("T:Fixture.Geometry.Circle", "Area").Modifiers);
        Assert.Equal(["sealed"], circle.Modifiers);
    }

    [Fact]
    public void VariablesCarryVisibilityAndModifiers()
    {
        CatalogVariable answer = FixtureCatalog.TypeOf(Result, "T:Fixture.Utilities.Helpers").Variables?.Single(v => v.Name == "Answer")
            ?? throw new Xunit.Sdk.XunitException("Answer missing");
        Assert.Equal(CatalogVariableKind.Field, answer.Kind);
        Assert.Equal(["static", "const"], answer.Modifiers);
        Assert.Equal(CatalogVisibility.Public, answer.Get);
        Assert.Null(answer.Set);

        CatalogVariable count = FixtureCatalog.TypeOf(Result, "T:Fixture.Utilities.Counter").Variables?.Single(v => v.Name == "Count")
            ?? throw new Xunit.Sdk.XunitException("Count missing");
        Assert.Equal(CatalogVariableKind.Property, count.Kind);
        Assert.Equal(CatalogVisibility.Public, count.Set);
    }

    [Fact]
    public void AnnotatedMembersAppearAndNetPrintsIgnoreHidesMembers()
    {
        CatalogType counter = FixtureCatalog.TypeOf(Result, "T:Fixture.Utilities.Counter");
        Assert.DoesNotContain(counter.Variables ?? [], v => v.Name == "Secret");
        Assert.DoesNotContain(counter.Methods ?? [], m => m.Name == "Reset");
        Assert.Contains(counter.Methods ?? [], m => m.Name == "Step");
        Assert.DoesNotContain(FixtureCatalog.TypeOf(Result, "T:Fixture.Utilities.Helpers").Methods ?? [], m => m.Name == "Hidden");
        Assert.Contains(Result.Document.Types, type => type.Id == "T:Fixture.Utilities.Counter.StepKind");
    }

    [Fact]
    public void SummariesAndParameterDocumentationComeFromTheXmlFile()
    {
        CatalogMethod tryParse = Method("T:Fixture.Utilities.Helpers", "TryParse");
        Assert.Equal("Parses text, returning success and the value through an out parameter.", tryParse.Summary);
        Assert.Equal("True when parsing succeeded.", tryParse.ReturnSummary);
        Assert.Equal("The parsed value.", tryParse.Parameters?[1].Summary);
        Assert.Equal("A two-dimensional vector: struct, operators, implicit conversions, interface implementation.", FixtureCatalog.TypeOf(Result, "T:Fixture.Geometry.Vector2").Summary);
    }
}
