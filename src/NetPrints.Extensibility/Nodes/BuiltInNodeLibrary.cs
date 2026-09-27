using NetPrints.Core;
using NetPrints.Graph;
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
            Describe("forLoop", Code, Suggest("For Loop", "Loop_16x.png", g => new ForLoopNode(g))),
            Describe("ifElse", Code, Suggest("If Else", "If_16x.png", g => new IfElseNode(g))),
            Describe("constructor", Code, Suggest("Construct New Object", "Create_16x.png",
                g => new ConstructorNode(g, new ConstructorSpecifier([], TypeSpecifier.FromType<object>())))),
            Describe("typeOf", Code, Suggest("Type Of", "Type_16x.png", g => new TypeOfNode(g))),
            Describe("explicitCast", Code, Suggest("Explicit Cast", "Convert_16x.png", g => new ExplicitCastNode(g))),
            Describe("return", GraphKinds.Method, Suggest("Return", "Return_16x.png", g => new ReturnNode(AsMethodGraph(g)))),
            Describe("makeArray", Code, Suggest("Make Array", "ListView_16x.png", g => new MakeArrayNode(g))),
            Describe("literal", Code, Suggest("Literal", "Literal_16x.png", g => new LiteralNode(g, TypeSpecifier.FromType<object>()))),
            Describe("type", CodeAndClass, Suggest("Type", "Type_16x.png", g => new TypeNode(g, TypeSpecifier.FromType<object>()))),
            Describe("makeArrayType", CodeAndClass, Suggest("Make Array Type", "Type_16x.png", g => new MakeArrayTypeNode(g))),
            Describe("throw", Code, Suggest("Throw", "Throw_16x.png", g => new ThrowNode(g))),
            Describe("await", GraphKinds.Method, Suggest("Await", "Task_16x.png", g => new AwaitNode(g))),
            Describe("ternary", Code, Suggest("Ternary", "ConditionalRule_16x.png", g => new TernaryNode(g))),
            Describe("default", Code, Suggest("Default", "None_16x.png", g => new DefaultNode(g))),
            Describe("methodEntry", GraphKinds.Method),
            Describe("constructorEntry", GraphKinds.Constructor),
            Describe("classReturn", GraphKinds.Class),
            Describe("typeReturn", GraphKinds.Type),
            Describe("callMethod", Code),
            Describe("makeDelegate", Code),
            Describe("variableGetter", Code),
            Describe("variableSetter", Code),
            Describe("reroute", Code),
        ];
    }

    private sealed class Library : INodeLibrary
    {
        public string Id => BuiltInNodeLibrary.Id;

        public IReadOnlyList<NodeKindDescriptor> NodeKinds { get; } = BuildKinds();
    }
}
