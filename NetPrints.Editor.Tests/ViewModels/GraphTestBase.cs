using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Tests.Fakes;
using NetPrints.Editor.ViewModels;

namespace NetPrints.Editor.Tests.ViewModels;

/// <summary>
/// A class with an open method graph, for graph/node/pin tests.
/// </summary>
public abstract class GraphTestBase
{
    protected TestEditor Editor = null!;
    protected ClassGraph Class = null!;
    protected MethodGraph Method = null!;
    protected ClassEditorVM ClassEditor = null!;
    protected NodeGraphVM Graph = null!;

    protected static readonly TypeSpecifier StringType = TypeSpecifier.FromType<string>();
    protected static readonly TypeSpecifier IntType = TypeSpecifier.FromType<int>();

    [TestInitialize]
    public void SetupGraph()
    {
        Editor = new TestEditor();
        Class = new ClassGraph { Name = "C", Namespace = "N" };
        ClassEditor = new ClassEditorVM(Class, Editor.Context);
        ClassEditor.CreateMethodCommand.Execute(null);
        Method = (MethodGraph)ClassEditor.Methods.Single().Graph;
        Graph = ClassEditor.OpenedGraph!;
    }

    [TestCleanup]
    public void CleanupGraph() => ClassEditor.Dispose();

    protected NodeVM VmOf(Node node) => Graph.Nodes.Single(n => n.Node == node);

    protected NodePinVM VmOf(NodePin pin) => Graph.Nodes.SelectMany(n => n.AllPins).Single(p => p.Pin == pin);

    protected static MethodSpecifier ConsoleWriteLine(TypeSpecifier parameterType) =>
        new("WriteLine", [new MethodParameter("value", parameterType, MethodParameterPassType.Default, false, null)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Console)), []);

    protected MethodSpecifier FindMethod(Type type, string name, params Type[] parameterTypes)
    {
        var provider = Editor.Reflection.Provider;
        return provider.GetMethods(new NetPrints.Reflection.ReflectionProviderMethodQuery().WithType(TypeSpecifier.FromType(type)))
            .First(m => m.Name == name && m.Parameters.Select(p => p.Value).SequenceEqual(parameterTypes.Select(t => (BaseType)TypeSpecifier.FromType(t))));
    }
}
