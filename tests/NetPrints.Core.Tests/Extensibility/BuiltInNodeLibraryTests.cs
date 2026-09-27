using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Graph;
using NetPrints.Serialization.Mapping;
using NetPrints.Translator;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility;

/// <summary>EX-T12: the built-in node kinds come through <see cref="BuiltInNodeLibrary"/>.</summary>
public class BuiltInNodeLibraryTests
{
    private static readonly string[] MethodSuggestions =
    [
        "For Loop", "If Else", "Construct New Object", "Type Of", "Explicit Cast", "Return", "Make Array",
        "Literal", "Type", "Make Array Type", "Throw", "Await", "Ternary", "Default",
    ];

    private static readonly string[] ConstructorSuggestions =
    [
        "For Loop", "If Else", "Construct New Object", "Type Of", "Explicit Cast", "Make Array",
        "Literal", "Type", "Make Array Type", "Throw", "Ternary", "Default",
    ];

    private static ExtensionRegistry LoadBuiltIn() => Load(ExtensionLoaderOptions.BuiltInOnly);

    [Fact]
    public async Task RegistryHoldsOneKindPerBuiltInConverter()
    {
        // eventEntry (sub-phase G, T080) is the 24th kind of document-format.md §1.5.
        await using ExtensionRegistry registry = LoadBuiltIn();

        Assert.Equal(NodeDocumentConverterRegistry.BuiltIn.Count, registry.NodeKinds.Count);
        Assert.Equal(24, registry.NodeKinds.Count);
        Assert.Equal(registry.NodeKinds.Count, registry.NodeKinds.Select(k => k.Kind).Distinct().Count());
        Assert.All(registry.NodeKinds, kind => Assert.False(kind.Kind.Contains('/')));
        Assert.All(registry.NodeKinds, kind => Assert.Same(kind.Converter, registry.NodeConverters.FindByKind(kind.Kind)));
        Assert.All(registry.NodeKinds, kind => Assert.Same(kind.Translator, registry.Translation.Nodes.Find(kind.NodeType)));
        Assert.Equal("netprints", Assert.Single(registry.Loaded).Id);
        Assert.Empty(registry.Issues);
    }

    [Fact]
    public async Task RemovingTheBuiltInExtensionMakesBuiltInNodesUnknown()
    {
        await using ExtensionRegistry registry = Load(Options());

        Assert.Empty(registry.NodeKinds);
        Assert.Null(registry.NodeConverters.FindByKind("ifElse"));
        Assert.Null(registry.Translation.Nodes.Find(typeof(IfElseNode)));
        Assert.Empty(registry.Loaded);
    }

    [Fact]
    public async Task TranslationEnvironmentReusesTheBuiltInTranslators()
    {
        await using ExtensionRegistry registry = LoadBuiltIn();

        Assert.Same(NodeTranslatorRegistry.BuiltIn.Find(typeof(CallMethodNode)), registry.Translation.Nodes.Find(typeof(CallMethodNode)));
        Assert.Same(NodeTranslatorRegistry.BuiltIn.Find(typeof(ForLoopNode)), registry.Translation.Nodes.Find(typeof(ForLoopNode)));
    }

    private class NoContext : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args) => throw new NotSupportedException();
    }

    [Theory]
    [InlineData("constructorEntry")]
    [InlineData("classReturn")]
    [InlineData("typeReturn")]
    [InlineData("type")]
    [InlineData("makeArrayType")]
    public async Task KindsTheExecutionTranslatorNeverHandlesFailWithNpt006(string kindName)
    {
        await using ExtensionRegistry registry = LoadBuiltIn();
        var kind = registry.NodeKinds.Single(k => k.Kind == kindName);
        Assert.Null(NodeTranslatorRegistry.BuiltIn.Find(kind.NodeType));
        var graph = new MethodGraph("M");
        Node node = kind.NodeType == typeof(TypeNode)
            ? new TypeNode(graph, TypeSpecifier.FromType<object>())
            : kind.NodeType == typeof(MakeArrayTypeNode) ? new MakeArrayTypeNode(graph) : graph.EntryNode;

        var exception = Assert.Throws<TranslationException>(() => kind.Translator.Translate(DispatchProxy.Create<IExecutionTranslationContext, NoContext>(), node, 0));

        Assert.Equal("NPT006", exception.Code);
        Assert.Equal(node.Id, exception.NodeId);
    }

    [Fact]
    public void SuggestionsAreOfferedPerGraphKindInTheOriginalOrder()
    {
        INodeLibrary library = BuiltInNodeLibrary.Instance;

        string[] For(GraphKinds graphKind) =>
            [.. library.NodeKinds.Where(k => k.AllowedIn.HasFlag(graphKind)).SelectMany(k => k.Suggestions).Select(s => s.DisplayName)];

        Assert.Equal(MethodSuggestions, For(GraphKinds.Method));
        Assert.Equal(ConstructorSuggestions, For(GraphKinds.Constructor));
        Assert.Equal(["Type", "Make Array Type"], For(GraphKinds.Class));
        Assert.Empty(For(GraphKinds.Type));
        Assert.All(library.NodeKinds.SelectMany(k => k.Suggestions), s => Assert.Equal("NetPrints", s.Category));
    }

    [Fact]
    public void SuggestionsKeepTheirIcons()
    {
        var suggestions = BuiltInNodeLibrary.Instance.NodeKinds.SelectMany(k => k.Suggestions).ToDictionary(s => s.DisplayName, s => s.IconKey);

        Assert.Equal("Loop_16x.png", suggestions["For Loop"]);
        Assert.Equal("Create_16x.png", suggestions["Construct New Object"]);
        Assert.Equal("ConditionalRule_16x.png", suggestions["Ternary"]);
        Assert.Equal("Task_16x.png", suggestions["Await"]);
    }

    [Fact]
    public void EverySuggestionCreatesItsNodeInAnAllowedGraph()
    {
        foreach (var kind in BuiltInNodeLibrary.Instance.NodeKinds.Where(k => k.Suggestions.Count > 0))
        {
            var graph = new MethodGraph("M");
            int before = graph.Nodes.Count;

            Node node = kind.Suggestions[0].Create(graph);

            Assert.IsType(kind.NodeType, node);
            Assert.Equal(before + 1, graph.Nodes.Count);
            Assert.Contains(node, graph.Nodes);
        }
    }

    [Fact]
    public void ReturnSuggestionNeedsAMethodGraph()
    {
        var kind = BuiltInNodeLibrary.Instance.NodeKinds.Single(k => k.Kind == "return");

        Assert.Throws<ArgumentException>(() => kind.Suggestions[0].Create(new ClassGraph()));
    }

    [Fact]
    public void GraphKindsAreMappedFromTheGraphType()
    {
        Assert.Equal(GraphKinds.Method, NodeGraphKinds.Of(new MethodGraph("M")));
        Assert.Equal(GraphKinds.Constructor, NodeGraphKinds.Of(new ConstructorGraph()));
        Assert.Equal(GraphKinds.Class, NodeGraphKinds.Of(new ClassGraph()));
    }

    [Fact]
    public void BuiltInExtensionUsesTheBuiltInId()
    {
        Assert.Equal(BuiltInNodeLibrary.Id, BuiltInExtension.Manifest.Id);
        Assert.Equal(new Version(1, 0), BuiltInExtension.Manifest.ApiVersion);
        Assert.Same(BuiltInExtension.Manifest, BuiltInExtension.InProcessEntry.Manifest);
    }
}
