using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Serialization.Documents;
using NetPrints.Serialization.Mapping;
using NetPrints.Translator;

namespace NetPrints.Extensibility.Nodes;

/// <summary>
/// The built-in node kinds (document-format.md §1.5) as a node library: each converter of
/// <see cref="NodeDocumentConverterRegistry.BuiltIn"/> paired with its built-in translator, the graph kinds it is
/// offered in and its node-search entry (PAR-52, PAR-53).
/// </summary>
public static class BuiltInNodeLibrary
{
    /// <summary>
    /// The library id, also the id of <see cref="BuiltInExtension"/>.
    /// </summary>
    public const string Id = "netprints";

    private const string Category = "NetPrints";

    private const GraphKinds Code = GraphKinds.Method | GraphKinds.Constructor;

    private const GraphKinds CodeAndClass = GraphKinds.Method | GraphKinds.Constructor | GraphKinds.Class;

    /// <summary>
    /// The library. It holds one descriptor per built-in converter, so <c>eventEntry</c> joins it with the
    /// converter (sub-phase G).
    /// </summary>
    public static INodeLibrary Instance { get; } = new Library();

    private static NodeKindDescriptor[] BuildKinds()
    {
        IReadOnlyList<INodeDocumentConverter> converters = NodeDocumentConverterRegistry.BuiltIn;

        NodeKindDescriptor Describe(string kind, GraphKinds allowedIn, NodeSuggestion? suggestion = null)
        {
            INodeDocumentConverter converter = converters.Single(c => c.Kind == kind);
            INodeTranslator translator = NodeTranslatorRegistry.BuiltIn.Find(converter.NodeType) ?? UntranslatedNodeTranslator.Instance;
            return new NodeKindDescriptor(kind, converter.NodeType, converter, translator, allowedIn, suggestion is null ? [] : [suggestion]);
        }

        NodeSuggestion Suggest(string name, string icon, Func<NodeGraph, Node> create) => new(Category, name, icon, create);

        MethodGraph AsMethodGraph(NodeGraph graph) =>
            graph as MethodGraph ?? throw new ArgumentException("A return node needs a method graph.", nameof(graph));

        // Order of the suggestions per graph kind is the order of PAR-52's built-in list.
        return
        [
            Describe(BuiltInNodeKinds.ForLoop, Code, Suggest("For Loop", "netprints.icon.category.loop", g => new ForLoopNode(g))),
            Describe(BuiltInNodeKinds.IfElse, Code, Suggest("If Else", "netprints.icon.category.if", g => new IfElseNode(g))),
            Describe(BuiltInNodeKinds.Constructor, Code, Suggest("Construct New Object", "netprints.icon.category.create",
                g => new ConstructorNode(g, new ConstructorSpecifier([], TypeSpecifier.FromType<object>())))),
            Describe(BuiltInNodeKinds.TypeOf, Code, Suggest("Type Of", "netprints.icon.category.type", g => new TypeOfNode(g))),
            Describe(BuiltInNodeKinds.ExplicitCast, Code, Suggest("Explicit Cast", "netprints.icon.category.convert", g => new ExplicitCastNode(g))),
            Describe(BuiltInNodeKinds.Return, GraphKinds.Method, Suggest("Return", "netprints.icon.category.return", g => new ReturnNode(AsMethodGraph(g)))),
            Describe(BuiltInNodeKinds.MakeArray, Code, Suggest("Make Array", "netprints.icon.category.listView", g => new MakeArrayNode(g))),
            Describe(BuiltInNodeKinds.Literal, Code, Suggest("Literal", "netprints.icon.category.literal", g => new LiteralNode(g, TypeSpecifier.FromType<object>()))),
            Describe(BuiltInNodeKinds.Type, CodeAndClass, Suggest("Type", "netprints.icon.category.type", g => new TypeNode(g, TypeSpecifier.FromType<object>()))),
            Describe(BuiltInNodeKinds.MakeArrayType, CodeAndClass, Suggest("Make Array Type", "netprints.icon.category.type", g => new MakeArrayTypeNode(g))),
            Describe(BuiltInNodeKinds.Throw, Code, Suggest("Throw", "netprints.icon.category.throw", g => new ThrowNode(g))),
            Describe(BuiltInNodeKinds.Await, GraphKinds.Method, Suggest("Await", "netprints.icon.category.task", g => new AwaitNode(g))),
            Describe(BuiltInNodeKinds.Ternary, Code, Suggest("Ternary", "netprints.icon.category.conditionalRule", g => new TernaryNode(g))),
            Describe(BuiltInNodeKinds.Default, Code, Suggest("Default", "netprints.icon.category.none", g => new DefaultNode(g))),
            Describe(BuiltInNodeKinds.MethodEntry, GraphKinds.Method),
            Describe(BuiltInNodeKinds.ConstructorEntry, GraphKinds.Constructor),
            Describe(BuiltInNodeKinds.EventEntry, GraphKinds.Event),
            Describe(BuiltInNodeKinds.ClassReturn, GraphKinds.Class),
            Describe(BuiltInNodeKinds.TypeReturn, GraphKinds.Type),
            Describe(BuiltInNodeKinds.CallMethod, Code),
            Describe(BuiltInNodeKinds.MakeDelegate, Code),
            Describe(BuiltInNodeKinds.VariableGetter, Code),
            Describe(BuiltInNodeKinds.VariableSetter, Code),
            Describe(BuiltInNodeKinds.Reroute, Code),
        ];
    }

    private sealed class Library : INodeLibrary
    {
        public string Id => BuiltInNodeLibrary.Id;

        public IReadOnlyList<NodeKindDescriptor> NodeKinds { get; } = BuildKinds();
    }
}
