using System.Globalization;
using NetPrints.Core;
using NetPrints.Editor.Search;

namespace NetPrints.Editor.Tests.Search;

public class MethodSignatureFormatterTests
{
    private static MethodParameter Param(string name, Type type, MethodParameterPassType pass = MethodParameterPassType.Default, bool isParams = false) =>
        new(name, TypeSpecifier.FromType(type), pass, false, null) { IsParams = isParams };

    private static MethodSpecifier Method(Type declaring, string name, Type? returns, params MethodParameter[] parameters) =>
        new(name, parameters, returns is null ? [] : [TypeSpecifier.FromType(returns)], MethodModifiers.Virtual, MemberVisibility.Public,
            TypeSpecifier.FromType(declaring), []);

    [Fact]
    public void AMethodWithNoParametersGivesItsReturnTypeFirst() =>
        Assert.Equal("string ToString()", MethodSignatureFormatter.Format(Method(typeof(object), "ToString", typeof(string))));

    [Fact]
    public void ParametersKeepTheirNamesAndTypeNamesDropNamespaces() =>
        Assert.Equal("void WriteLine(string format, object arg0)",
            MethodSignatureFormatter.Format(Method(typeof(Console), "WriteLine", null, Param("format", typeof(string)), Param("arg0", typeof(object)))));

    [Fact]
    public void ALongTypeNameLosesItsNamespace() =>
        Assert.Equal("void GetObjectData(SerializationInfo info, StreamingContext context)",
            MethodSignatureFormatter.Format(Method(typeof(Exception), "GetObjectData", null,
                Param("info", typeof(System.Runtime.Serialization.SerializationInfo)), Param("context", typeof(System.Runtime.Serialization.StreamingContext)))));

    [Theory]
    [InlineData(typeof(bool), "bool")]
    [InlineData(typeof(byte), "byte")]
    [InlineData(typeof(sbyte), "sbyte")]
    [InlineData(typeof(char), "char")]
    [InlineData(typeof(decimal), "decimal")]
    [InlineData(typeof(double), "double")]
    [InlineData(typeof(float), "float")]
    [InlineData(typeof(int), "int")]
    [InlineData(typeof(uint), "uint")]
    [InlineData(typeof(long), "long")]
    [InlineData(typeof(ulong), "ulong")]
    [InlineData(typeof(short), "short")]
    [InlineData(typeof(ushort), "ushort")]
    [InlineData(typeof(object), "object")]
    [InlineData(typeof(string), "string")]
    public void BuiltInTypesTakeTheirKeyword(Type type, string keyword) =>
        Assert.Equal($"{keyword} Get()", MethodSignatureFormatter.Format(Method(typeof(Exception), "Get", type)));

    [Fact]
    public void GenericArraysAndNullableTypesUseCSharpForm()
    {
        MethodSpecifier method = Method(typeof(Exception), "Get", typeof(Dictionary<string, List<int>>),
            Param("list", typeof(List<int>)), Param("numbers", typeof(int[])), Param("maybe", typeof(int?)), Param("grid", typeof(int[][])));

        Assert.Equal("Dictionary<string, List<int>> Get(List<int> list, int[] numbers, int? maybe, int[][] grid)", MethodSignatureFormatter.Format(method));
    }

    [Fact]
    public void PassModifiersAreKept()
    {
        MethodSpecifier method = Method(typeof(Exception), "Try", typeof(bool),
            Param("a", typeof(int), MethodParameterPassType.Reference),
            Param("b", typeof(int), MethodParameterPassType.Out),
            Param("c", typeof(int), MethodParameterPassType.In),
            Param("d", typeof(int[]), isParams: true));

        Assert.Equal("bool Try(ref int a, out int b, in int c, params int[] d)", MethodSignatureFormatter.Format(method));
    }

    [Fact]
    public void AGenericMethodListsItsTypeParameters()
    {
        var t = new GenericType("T");
        var predicate = new TypeSpecifier("System.Predicate", false, false, [t]);
        var method = new MethodSpecifier("Find", [new MethodParameter("match", predicate, MethodParameterPassType.Default, false, null)], [t],
            MethodModifiers.None, MemberVisibility.Public, TypeSpecifier.FromType(typeof(List<int>)), [t]);

        Assert.Equal("T Find<T>(Predicate<T> match)", MethodSignatureFormatter.Format(method));
    }

    [Fact]
    public void AConstructorShowsItsTypeNameAndParameters()
    {
        var constructor = new ConstructorSpecifier([Param("capacity", typeof(int))], TypeSpecifier.FromType<System.Text.StringBuilder>());

        Assert.Equal("StringBuilder(int capacity)", MethodSignatureFormatter.Format(constructor));
    }

    [Fact]
    public void TheDeclaringTypeIsItsShortName()
    {
        Assert.Equal("Exception", MethodSignatureFormatter.DeclaringTypeName(Method(typeof(Exception), "ToString", typeof(string))));
        Assert.Equal("List<int>", MethodSignatureFormatter.DeclaringTypeName(Method(typeof(List<int>), "Clear", null)));
        Assert.Equal("StringBuilder", MethodSignatureFormatter.DeclaringTypeName(new ConstructorSpecifier([], TypeSpecifier.FromType<System.Text.StringBuilder>())));
    }

    [Fact]
    public void ANestedTypeKeepsItsOuterType() =>
        Assert.Equal("Outer.Inner Get()", MethodSignatureFormatter.Format(new MethodSpecifier("Get", [], [new TypeSpecifier("My.Space.Outer+Inner")],
            MethodModifiers.None, MemberVisibility.Public, TypeSpecifier.FromType<object>(), [])));

    [Fact]
    public void TheOutputIsTheSameUnderAnyCulture()
    {
        MethodSpecifier method = Method(typeof(Console), "WriteLine", typeof(int), Param("I", typeof(int)), Param("index", typeof(double?)));
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            string turkish = MethodSignatureFormatter.Format(method);
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            string german = MethodSignatureFormatter.Format(method);

            Assert.Equal("int WriteLine(int I, double? index)", turkish);
            Assert.Equal(turkish, german);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
