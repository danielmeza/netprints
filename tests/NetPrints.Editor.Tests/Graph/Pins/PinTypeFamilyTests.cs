using NetPrints.Compilation;
using NetPrints.Core;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Tests.Reflection;
using NetPrints.Projects;
using NetPrints.Reflection;

namespace NetPrints.Editor.Tests.Graph.Pins;

/// <summary>A reflection provider over the runtime assembly set plus a project's own delegate, struct and class (FR-107).</summary>
public sealed class ProjectReflectionFixture
{
    public const string ProjectSource = "namespace Proj { public delegate void Handler(int x); public struct Point { public int X; } public class Widget { } }";

    public ProjectReflectionFixture()
    {
        var assemblies = TestSnapshots.RuntimeAssemblyPaths().Select(path => new ResolvedAssembly(path, null)).ToList();
        Provider = new ReflectionProvider(assemblies, [new SourceFile("Proj.cs", ProjectSource)], new HashSet<string>());
    }

    public IReflectionProvider Provider { get; }
}

/// <summary>The colour family of a pin, from its kind and its data type (FR-107, T092n).</summary>
public class PinTypeFamilyTests(ProjectReflectionFixture fixture) : IClassFixture<ProjectReflectionFixture>
{
    private static readonly TypeSpecifier ProjectDelegate = new("Proj.Handler");

    private static readonly TypeSpecifier ProjectStruct = new("Proj.Point");

    private static readonly TypeSpecifier ProjectClass = new("Proj.Widget");

    private static readonly (string Name, BaseType Type, PinTypeFamily Family)[] Table =
    [
        ("bool", TypeSpecifier.FromType<bool>(), PinTypeFamily.Bool),
        ("sbyte", TypeSpecifier.FromType<sbyte>(), PinTypeFamily.Integer),
        ("byte", TypeSpecifier.FromType<byte>(), PinTypeFamily.Integer),
        ("short", TypeSpecifier.FromType<short>(), PinTypeFamily.Integer),
        ("ushort", TypeSpecifier.FromType<ushort>(), PinTypeFamily.Integer),
        ("int", TypeSpecifier.FromType<int>(), PinTypeFamily.Integer),
        ("uint", TypeSpecifier.FromType<uint>(), PinTypeFamily.Integer),
        ("long", TypeSpecifier.FromType<long>(), PinTypeFamily.Integer),
        ("ulong", TypeSpecifier.FromType<ulong>(), PinTypeFamily.Integer),
        ("nint", TypeSpecifier.FromType<nint>(), PinTypeFamily.Integer),
        ("nuint", TypeSpecifier.FromType<nuint>(), PinTypeFamily.Integer),
        ("float", TypeSpecifier.FromType<float>(), PinTypeFamily.Float),
        ("double", TypeSpecifier.FromType<double>(), PinTypeFamily.Float),
        ("decimal", TypeSpecifier.FromType<decimal>(), PinTypeFamily.Float),
        ("string", TypeSpecifier.FromType<string>(), PinTypeFamily.String),
        ("char", TypeSpecifier.FromType<char>(), PinTypeFamily.String),
        ("Action", TypeSpecifier.FromType<Action>(), PinTypeFamily.Delegate),
        ("Func<int>", TypeSpecifier.FromType<Func<int>>(), PinTypeFamily.Delegate),
        ("EventHandler", TypeSpecifier.FromType<EventHandler>(), PinTypeFamily.Delegate),
        ("project delegate", ProjectDelegate, PinTypeFamily.Delegate),
        ("DateTime", TypeSpecifier.FromType<DateTime>(), PinTypeFamily.ValueType),
        ("DayOfWeek", TypeSpecifier.FromType<DayOfWeek>(), PinTypeFamily.ValueType),
        ("project struct", ProjectStruct, PinTypeFamily.ValueType),
        ("object", TypeSpecifier.FromType<object>(), PinTypeFamily.Object),
        ("Exception", TypeSpecifier.FromType<Exception>(), PinTypeFamily.Object),
        ("IDisposable", TypeSpecifier.FromType<IDisposable>(), PinTypeFamily.Object),
        ("int[]", TypeSpecifier.FromType<int[]>(), PinTypeFamily.Object),
        ("List<int>", TypeSpecifier.FromType<List<int>>(), PinTypeFamily.Object),
        ("Task<int>", TypeSpecifier.FromType<Task<int>>(), PinTypeFamily.Object),
        ("project class", ProjectClass, PinTypeFamily.Object),
        ("int?", TypeSpecifier.FromType<int?>(), PinTypeFamily.Integer),
        ("DateTime?", TypeSpecifier.FromType<DateTime?>(), PinTypeFamily.ValueType),
        ("T", new GenericType("T"), PinTypeFamily.Generic),
        ("unresolvable", new TypeSpecifier("Missing.Nothing"), PinTypeFamily.Generic),
    ];

    public static TheoryData<string> Names() => [.. Table.Select(row => row.Name)];

    [Theory]
    [MemberData(nameof(Names))]
    public void ADataPinTakesTheFamilyOfItsType(string name)
    {
        var row = Table.Single(r => r.Name == name);

        Assert.Equal(row.Family.Name, PinTypeFamily.Of(PinKind.Data, row.Type, fixture.Provider).Name);
    }

    [Fact]
    public void ExecutionAndTypePinsHaveTheirOwnFamily()
    {
        Assert.Same(PinTypeFamily.Exec, PinTypeFamily.Of(PinKind.Exec, null, fixture.Provider));
        Assert.Same(PinTypeFamily.Type, PinTypeFamily.Of(PinKind.Type, null, fixture.Provider));
        Assert.Same(PinTypeFamily.Exec, PinTypeFamily.Of(PinKind.Exec, TypeSpecifier.FromType<int>(), null));
    }

    [Fact]
    public void ADataPinWithoutAResolvedTypeOrAProviderIsGenericUnlessItsNameIsBuiltIn()
    {
        Assert.Same(PinTypeFamily.Generic, PinTypeFamily.Of(PinKind.Data, null, fixture.Provider));
        Assert.Same(PinTypeFamily.Generic, PinTypeFamily.Of(PinKind.Data, TypeSpecifier.FromType<List<int>>(), null));
        Assert.Same(PinTypeFamily.Integer, PinTypeFamily.Of(PinKind.Data, TypeSpecifier.FromType<int>(), null));
    }

    [Fact]
    public void EveryFamilyHasOneStyleClassAndOneTokenAndTheyAreAllListed()
    {
        Assert.Equal(
            ["Exec", "Type", "Bool", "Integer", "Float", "String", "Object", "ValueType", "Delegate", "Generic"],
            PinTypeFamily.All.Select(family => family.Name));
        Assert.All(PinTypeFamily.All, family =>
        {
            Assert.Equal("pin-" + family.Name.ToLowerInvariant(), family.StyleClass);
            Assert.Equal("Pin." + family.Name, family.TokenKey);
        });
        Assert.Equal(PinTypeFamily.All.Count, PinTypeFamily.All.Select(family => family.StyleClass).Distinct().Count());
    }
}
