using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using NetPrints.Core;
using NetPrints.Editor.Search;
using NetPrints.Reflection;

namespace NetPrints.Editor.Tests.Reflection;

/// <summary>Array parameters keep their element type and rank through the real reflection provider (FR-093, SC-012).</summary>
public class ArrayTypeConversionTests(RuntimeReflectionFixture fixture) : IClassFixture<RuntimeReflectionFixture>
{
    private const string Source = """
        using System.Collections.Generic;
        public class Holder<T>
        {
            public void Many(char[] chars, int[,] grid, int[][] jagged, int[][,] jaggedGrid, int[,][] gridOfArrays, List<int>[] lists, Dictionary<string, List<int>[]> map, string[][] names) { }

            public void Open(T[] items) { }
        }
        """;

    private static readonly Lazy<IMethodSymbol> Many = new(() => Method("Many"));

    private IReflectionProvider Provider => fixture.Provider;

    private static IMethodSymbol Method(string name)
    {
        var tree = CSharpSyntaxTree.ParseText(Source, cancellationToken: TestContext.Current.CancellationToken);
        var references = TestSnapshots.RuntimeAssemblyPaths().Select(path => MetadataReference.CreateFromFile(path));
        var compilation = CSharpCompilation.Create("Arrays", [tree], references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        INamedTypeSymbol holder = compilation.GetTypeByMetadataName("Holder`1") ?? throw new InvalidOperationException("Holder is missing.");
        return holder.GetMembers(name).OfType<IMethodSymbol>().Single();
    }

    private List<string> Overloads(Type type, string name) =>
        [.. Provider.GetMethods(new ReflectionProviderMethodQuery().WithType(TypeSpecifier.FromType(type)).WithStatic(true))
            .Where(method => method.Name == name)
            .Select(MethodSignatureFormatter.Format)
            .Order(StringComparer.Ordinal)];

    [Fact]
    public void ConsoleWriteLineShowsTheCharArrayParameter() =>
        Assert.Contains("void WriteLine(char[] buffer)", Overloads(typeof(Console), "WriteLine"));

    [Fact]
    public void StringJoinOverloadsDifferByTheirElementType()
    {
        List<string> rows = Overloads(typeof(string), "Join");

        Assert.Contains("string Join(string separator, params string[] value)", rows);
        Assert.Contains("string Join(string separator, params object[] values)", rows);
        Assert.DoesNotContain(rows, row => row.Contains("Array", StringComparison.Ordinal));
    }

    [Fact]
    public void AnArrayParameterIsFoundByItsElementTypeAndRank()
    {
        var query = new ReflectionProviderMethodQuery().WithType(TypeSpecifier.FromType(typeof(Console))).WithStatic(true).WithArgumentType(new TypeSpecifier("System.Char[]"));

        Assert.Contains(Provider.GetMethods(query), method => method.Name == "WriteLine" && method.ArgumentTypes.Count == 1);
    }

    [Theory]
    [InlineData("System.Char[]")]
    [InlineData("System.Int32[,]")]
    [InlineData("System.Int32[][,]")]
    public void ASpecifierWithBracketsResolvesBackToAnArrayType(string array) =>
        Assert.True(Provider.TypeSpecifierIsSubclassOf(new TypeSpecifier(array), new TypeSpecifier("System.Array")));

    [Fact]
    public void ConvertedArrayParametersKeepElementTypeAndRank()
    {
        string[] names = [.. Many.Value.Parameters.Select(parameter => ReflectionConverter.TypeSpecifierFromSymbol(parameter.Type).Name)];

        Assert.Equal(
            ["System.Char[]", "System.Int32[,]", "System.Int32[][]", "System.Int32[][,]", "System.Int32[,][]", "System.Collections.Generic.List[]", "System.Collections.Generic.Dictionary", "System.String[][]"],
            names);
    }

    [Fact]
    public void AGenericElementKeepsItsArgumentsOnTheArraySpecifier()
    {
        TypeSpecifier lists = ReflectionConverter.TypeSpecifierFromSymbol(Many.Value.Parameters[5].Type);
        TypeSpecifier map = ReflectionConverter.TypeSpecifierFromSymbol(Many.Value.Parameters[6].Type);

        Assert.Equal([TypeSpecifier.FromType<int>()], lists.GenericArguments);
        Assert.Equal("System.Collections.Generic.List[]", Assert.IsType<TypeSpecifier>(map.GenericArguments[1]).Name);
        Assert.Equal("System.Collections.Generic.List<System.Int32>[]", Assert.IsType<TypeSpecifier>(map.GenericArguments[1]).FullCodeName);
    }

    [Fact]
    public void ACodeNamePutsTheArraySuffixAfterTheGenericArguments() =>
        Assert.Equal("System.Collections.Generic.List<System.Int32>[][,]",
            new TypeSpecifier("System.Collections.Generic.List[][,]", false, false, [TypeSpecifier.FromType<int>()]).FullCodeName);

    [Fact]
    public void AnArrayOfATypeParameterStaysAnArray()
    {
        IParameterSymbol items = Method("Open").Parameters.Single();

        Assert.Equal("System.Array", Assert.IsType<TypeSpecifier>(ReflectionConverter.BaseTypeSpecifierFromSymbol(items.Type)).Name);
    }
}
