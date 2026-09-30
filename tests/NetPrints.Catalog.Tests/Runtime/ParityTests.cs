using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Reflection;
using Xunit;

namespace NetPrints.Catalog.Tests.Runtime;

/// <summary>
/// CT-T08: the live <see cref="ReflectionProvider"/> over the fixture library and a <see cref="CatalogTypeCatalog"/> over its
/// <c>public-api</c> catalog answer every query alike. The live side is narrowed to what a catalog can know: members declared by a
/// cataloged type that are public or protected (the live provider also lists private members and everything it inherits from
/// <c>System.Object</c>, <c>ValueType</c> and <c>Enum</c>). Results are compared as sets, as the composite provider drops duplicates.
/// </summary>
public sealed class ParityTests
{
    private static readonly CatalogDocument Document = FixtureCatalog.BuildFixture(BuiltInCatalogProfiles.PublicApi, FixtureCatalog.FixtureId).Document;

    private static readonly ITypeCatalog Catalog = CatalogLoader.Load(Document);

    private static readonly ReflectionProvider Live = CreateLive();

    private static readonly IReadOnlyDictionary<string, CatalogType> ById = Document.Types.ToDictionary(type => type.Id, StringComparer.Ordinal);

    private static readonly IReadOnlyList<TypeSpecifier> TypeSpecs = [.. Document.Types.Select(SpecifierOf)];

    private static readonly HashSet<string> CatalogedNames = [.. TypeSpecs.Select(spec => spec.Name)];

    // The live provider looks types up by metadata name, which loses the arity of an enclosing generic type: it cannot find these.
    private static readonly IReadOnlyList<TypeSpecifier> Resolvable =
        [.. Document.Types.Where(type => !IsNestedInGeneric(type)).Select(SpecifierOf)];

    private static readonly IReadOnlyList<TypeSpecifier> Externals =
    [
        new("System.Object"),
        new("System.ValueType"),
        new("System.Enum"),
        new("System.IComparable", isInterface: true),
        new("System.IEquatable", isInterface: true, genericArguments: [new TypeSpecifier("Fixture.Geometry.Vector2")]),
        new("System.Single"),
        new("System.Int32"),
        new("System.String"),
    ];

    private static ReflectionProvider CreateLive()
    {
        IReadOnlyList<string> framework = FixtureCatalog.FrameworkAssemblyPaths();
        List<ResolvedAssembly> assemblies = [.. framework.Select(path => new ResolvedAssembly(path, null)), new ResolvedAssembly(FixtureLibrary.AssemblyPath, FixtureLibrary.DocumentationPath)];
        HashSet<string> excluded = [.. framework.Select(path => Path.GetFileNameWithoutExtension(path))];
        return new ReflectionProvider(assemblies, [], excluded);
    }

    private static string FullName(CatalogType type) =>
        type.DeclaringType is { } outer ? FullName(ById[outer]) + "+" + type.Name : type.Namespace is null ? type.Name : type.Namespace + "." + type.Name;

    private static bool IsNestedInGeneric(CatalogType type) =>
        type.DeclaringType is { } outer && (ById[outer].GenericParameters is not null || IsNestedInGeneric(ById[outer]));

    private static TypeSpecifier SpecifierOf(CatalogType type) =>
        new(FullName(type), type.Kind == CatalogTypeKind.Enum, type.Kind == CatalogTypeKind.Interface, type.GenericParameters?.Select(name => (BaseType)new GenericType(name)));

    // Left out of every catalog on purpose: an obsolete member whose use is an error, and members marked [NetPrintsIgnore].
    private static readonly HashSet<string> ProfileExcluded = ["Fixture.Legacy.Old.Removed", "Fixture.Utilities.Counter.Reset", "Fixture.Utilities.Counter.Secret"];

    // D-R16: the live provider lists indexers (as a property named "this[]"); a catalog never does (Exposure.IsHiddenKind). Events are not listed by either side.
    private static readonly HashSet<string> NotCatalogedByDesign = ["Fixture.Utilities.Indexed.this[]"];

    private static bool Cataloged(TypeSpecifier? type) => type is not null && CatalogedNames.Contains(type.Name);

    private static bool Cataloged(MethodSpecifier method) =>
        Cataloged(method.DeclaringType) && Exposed(method.Visibility) && !ProfileExcluded.Contains($"{method.DeclaringType.Name.Replace('+', '.')}.{method.Name}");

    private static bool Cataloged(VariableSpecifier variable) =>
        Cataloged(variable.DeclaringType) && (Exposed(variable.GetterVisibility) || Exposed(variable.SetterVisibility))
        && !ProfileExcluded.Contains($"{variable.DeclaringType?.Name.Replace('+', '.')}.{variable.Name}")
        && !NotCatalogedByDesign.Contains($"{variable.DeclaringType?.Name.Replace('+', '.')}.{variable.Name}");

    private static bool Exposed(MemberVisibility visibility) => visibility.HasFlag(MemberVisibility.Public) || visibility.HasFlag(MemberVisibility.Protected);

    private static string Describe(BaseType type) => type switch
    {
        GenericType generic => "'" + generic.Name,
        TypeSpecifier specifier => specifier.Name + (specifier.IsEnum ? "#enum" : string.Empty) + (specifier.IsInterface ? "#interface" : string.Empty)
            + (specifier.GenericArguments.Count == 0 ? string.Empty : "<" + string.Join(",", specifier.GenericArguments.Select(Describe)) + ">"),
        _ => type.Name,
    };

    private static string Describe(MethodParameter parameter) =>
        $"{parameter.Name}:{Describe(parameter.Value)}:{parameter.PassType}:{(parameter.HasExplicitDefaultValue ? "default=" + DescribeValue(parameter.ExplicitDefaultValue) : "required")}";

    private static string DescribeValue(object? value) =>
        value is null ? "null" : value.GetType().FullName + ":" + Convert.ToString(value, CultureInfo.InvariantCulture);

    private static string Describe(MethodSpecifier method) =>
        $"{Describe(method.DeclaringType)}::{method.Name}<{string.Join(",", method.GenericArguments.Select(Describe))}>"
        + $"({string.Join(",", method.Parameters.Select(Describe))}):{string.Join(",", method.ReturnTypes.Select(Describe))} [{method.Modifiers}] {method.Visibility}";

    private static string Describe(VariableSpecifier variable) =>
        $"{(variable.DeclaringType is null ? "-" : Describe(variable.DeclaringType))}::{variable.Name}:{Describe(variable.Type)} get={variable.GetterVisibility} set={variable.SetterVisibility} vis={variable.Visibility} [{variable.Modifiers}]";

    private static string Describe(ConstructorSpecifier constructor) =>
        $"{Describe(constructor.DeclaringType)}({string.Join(",", constructor.Arguments.Select(Describe))})";

    private static IEnumerable<string> LiveMethods(ReflectionProviderMethodQuery query) =>
        Live.GetMethods(query).Where(Cataloged).Select(Describe);

    private static IEnumerable<string> CatalogMethods(ReflectionProviderMethodQuery query) => Catalog.GetMethods(query).Select(Describe);

    private static IEnumerable<string> LiveVariables(ReflectionProviderVariableQuery query) =>
        Live.GetVariables(query).Where(Cataloged).Select(Describe);

    private static IEnumerable<string> CatalogVariables(ReflectionProviderVariableQuery query) => Catalog.GetVariables(query).Select(Describe);

    private static void Compare(List<string> differences, string query, IEnumerable<string> live, IEnumerable<string> catalog)
    {
        string[] liveSet = [.. live.Distinct(StringComparer.Ordinal)];
        string[] catalogSet = [.. catalog.Distinct(StringComparer.Ordinal)];
        differences.AddRange(liveSet.Except(catalogSet, StringComparer.Ordinal).Select(item => $"only live: {item}   [{query}]"));
        differences.AddRange(catalogSet.Except(liveSet, StringComparer.Ordinal).Select(item => $"only catalog: {item}   [{query}]"));
    }

    private static void AssertNoDifferences(List<string> differences)
    {
        string[] distinct = [.. differences.GroupBy(difference => difference.Contains("   [", StringComparison.Ordinal) ? difference[..difference.LastIndexOf("   [", StringComparison.Ordinal)] : difference).Select(group => group.First())];
        Assert.True(distinct.Length == 0, $"{differences.Count} differences, {distinct.Length} distinct:{Environment.NewLine}{string.Join(Environment.NewLine, distinct.Take(30))}");
    }

    private static IEnumerable<TypeSpecifier?> QueryTypes() => new TypeSpecifier?[] { null }.Concat(Resolvable);

    private static readonly bool?[] Flags = [null, true, false];

    private static readonly TypeSpecifier?[] VisibleFrom = [null, new("Fixture.Geometry.Circle"), new("Fixture.Geometry.ShapeBase"), new("Fixture.Geometry.Vector2")];

    // Types outside a catalog are known by identity only, and an array parameter is System.Array in a catalog: neither String (which
    // implements IEnumerable<char>) nor System.Array can be compared with what the live provider knows about them.
    private static readonly TypeSpecifier[] SearchTypes =
    [
        new("Fixture.Geometry.Vector2"),
        new("Fixture.Geometry.Circle"),
        new("Fixture.Geometry.IShape", isInterface: true),
        new("System.Single"),
        new("System.Int32"),
        new("System.Object"),
    ];

    [Fact]
    public void TheComparisonCoversTheWholeFixture()
    {
        Assert.Equal(Document.Types.Count, Live.GetNonStaticTypes().Count(type => type.Name.StartsWith("Fixture.", StringComparison.Ordinal)));
        Assert.True(LiveMethods(new ReflectionProviderMethodQuery()).Count() >= 30);
        Assert.True(LiveVariables(new ReflectionProviderVariableQuery()).Count() >= 20);
        Assert.Contains(Document.Types, type => type.Kind == CatalogTypeKind.Enum);
        Assert.Contains(Document.Types, type => type.Kind == CatalogTypeKind.Interface);
        Assert.Contains(Document.Types, type => type.GenericParameters is not null);
        Assert.Contains(Document.Types, type => type.DeclaringType is not null);
    }

    [Fact]
    public void NonStaticTypesAreTheSame()
    {
        var differences = new List<string>();

        Compare(differences, "GetNonStaticTypes", Live.GetNonStaticTypes().Where(type => Cataloged(type) || type.Name.StartsWith("Fixture.", StringComparison.Ordinal)).Select(Describe), Catalog.GetNonStaticTypes().Select(Describe));

        AssertNoDifferences(differences);
    }

    [Fact]
    public void MethodsAreTheSameForEveryTypeStaticGenericAndVisibilityCombination()
    {
        var differences = new List<string>();
        foreach (TypeSpecifier? type in QueryTypes())
        {
            foreach (bool? isStatic in Flags)
            {
                foreach (bool? generic in Flags)
                {
                    foreach (TypeSpecifier? from in VisibleFrom)
                    {
                        var query = new ReflectionProviderMethodQuery { Type = type, Static = isStatic, HasGenericArguments = generic, VisibleFrom = from };
                        Compare(differences, $"GetMethods(type={type}, static={isStatic}, generic={generic}, from={from})", LiveMethods(query), CatalogMethods(query));
                    }
                }
            }
        }

        AssertNoDifferences(differences);
    }

    [Fact]
    public void MethodsAreTheSameForEveryArgumentAndReturnTypeFilter()
    {
        var differences = new List<string>();
        foreach (TypeSpecifier? type in QueryTypes())
        {
            foreach (bool? isStatic in Flags)
            {
                foreach (TypeSpecifier search in SearchTypes)
                {
                    var byArgument = new ReflectionProviderMethodQuery { Type = type, Static = isStatic, ArgumentType = search };
                    Compare(differences, $"GetMethods(type={type}, static={isStatic}, argument={search})", LiveMethods(byArgument), CatalogMethods(byArgument));
                    var byReturn = new ReflectionProviderMethodQuery { Type = type, Static = isStatic, ReturnType = search };
                    Compare(differences, $"GetMethods(type={type}, static={isStatic}, return={search})", LiveMethods(byReturn), CatalogMethods(byReturn));
                }
            }
        }

        AssertNoDifferences(differences);
    }

    [Fact]
    public void VariablesAreTheSameForEveryTypeStaticAndVisibilityCombination()
    {
        var differences = new List<string>();
        foreach (TypeSpecifier? type in QueryTypes())
        {
            foreach (bool? isStatic in Flags)
            {
                foreach (TypeSpecifier? from in VisibleFrom)
                {
                    var query = new ReflectionProviderVariableQuery { Type = type, Static = isStatic, VisibleFrom = from };
                    Compare(differences, $"GetVariables(type={type}, static={isStatic}, from={from})", LiveVariables(query), CatalogVariables(query));
                }
            }
        }

        AssertNoDifferences(differences);
    }

    [Fact]
    public void VariablesAreTheSameForEveryVariableTypeFilter()
    {
        var differences = new List<string>();
        foreach (TypeSpecifier? type in QueryTypes())
        {
            foreach (TypeSpecifier search in SearchTypes)
            {
                foreach (bool derives in new[] { true, false })
                {
                    var query = new ReflectionProviderVariableQuery { Type = type, VariableType = search, VariableTypeDerivesFrom = derives };
                    Compare(differences, $"GetVariables(type={type}, variableType={search}, derives={derives})", LiveVariables(query), CatalogVariables(query));
                }
            }
        }

        AssertNoDifferences(differences);
    }

    [Fact]
    public void ConstructorsEnumNamesOverridableMethodsAndOverloadsAreTheSame()
    {
        var differences = new List<string>();
        foreach (TypeSpecifier type in Resolvable)
        {
            Compare(differences, $"GetConstructors({type})", Live.GetConstructors(type).Select(Describe), Catalog.GetConstructors(type).Select(Describe));
            if (type.IsEnum)
            {
                Compare(differences, $"GetEnumNames({type})", Live.GetEnumNames(type), Catalog.GetEnumNames(type));
            }

            Compare(
                differences,
                $"GetOverridableMethodsForType({type})",
                Live.GetOverridableMethodsForType(type).Where(Cataloged).Select(Describe),
                Catalog.GetOverridableMethodsForType(type).Select(Describe));
        }

        foreach (MethodSpecifier method in Live.GetMethods(new ReflectionProviderMethodQuery()).Where(Cataloged))
        {
            if (Resolvable.Contains(method.DeclaringType))
            {
                Compare(
                    differences,
                    $"GetPublicMethodOverloads({Describe(method)})",
                    Live.GetPublicMethodOverloads(method).Where(Cataloged).Select(Describe),
                    Catalog.GetPublicMethodOverloads(method).Select(Describe));
            }
        }

        AssertNoDifferences(differences);
    }

    [Fact]
    public void DocumentationIsTheSameForEveryMethodParameterAndReturn()
    {
        var differences = new List<string>();
        int answered = 0;
        foreach (MethodSpecifier method in Live.GetMethods(new ReflectionProviderMethodQuery()).Where(method => Cataloged(method) && Resolvable.Contains(method.DeclaringType)))
        {
            string label = Describe(method);
            string? summary = Live.GetMethodDocumentation(method);
            answered += summary is null ? 0 : 1;
            if (summary != Catalog.GetMethodDocumentation(method))
            {
                differences.Add($"summary of {label}: live '{summary}', catalog '{Catalog.GetMethodDocumentation(method)}'");
            }

            if (Live.GetMethodReturnDocumentation(method, 0) != Catalog.GetMethodReturnDocumentation(method, 0))
            {
                differences.Add($"return of {label}: live '{Live.GetMethodReturnDocumentation(method, 0)}', catalog '{Catalog.GetMethodReturnDocumentation(method, 0)}'");
            }

            for (int index = 0; index < method.Parameters.Count; index++)
            {
                if (Live.GetMethodParameterDocumentation(method, index) != Catalog.GetMethodParameterDocumentation(method, index))
                {
                    differences.Add($"parameter {index} of {label}: live '{Live.GetMethodParameterDocumentation(method, index)}', catalog '{Catalog.GetMethodParameterDocumentation(method, index)}'");
                }
            }
        }

        AssertNoDifferences(differences);
        Assert.True(answered > 10, "The live provider answered too few summaries for the comparison to mean anything.");
    }

    [Fact]
    public void SubclassAndImplicitCastAnswersAreTheSameForEveryPairOfFixtureTypes()
    {
        var differences = new List<string>();
        IReadOnlyList<TypeSpecifier> targets = [.. Resolvable, .. Externals.Take(5)];
        foreach (TypeSpecifier from in Resolvable)
        {
            foreach (TypeSpecifier to in targets)
            {
                Check(differences, from, to);
                Check(differences, to, from);
            }
        }

        var single = new TypeSpecifier("System.Single");
        foreach (TypeSpecifier to in Resolvable.Append(new TypeSpecifier("System.Object")))
        {
            Check(differences, single, to);
            Check(differences, to, single);
        }

        AssertNoDifferences(differences);

        static void Check(List<string> differences, TypeSpecifier from, TypeSpecifier to)
        {
            if (Live.TypeSpecifierIsSubclassOf(from, to) != Catalog.TypeSpecifierIsSubclassOf(from, to))
            {
                differences.Add($"TypeSpecifierIsSubclassOf({from}, {to}): live {Live.TypeSpecifierIsSubclassOf(from, to)}");
            }

            if (Live.HasImplicitCast(from, to) != Catalog.HasImplicitCast(from, to))
            {
                differences.Add($"HasImplicitCast({from}, {to}): live {Live.HasImplicitCast(from, to)}");
            }
        }
    }
}
