using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Graph;

/// <summary>
/// A class with an open method graph, for graph/node/pin tests.
/// </summary>
public abstract class GraphTestBase : IDisposable
{
    protected readonly TestEditor Editor;
    protected readonly ClassGraph Class;
    protected readonly MethodGraph Method;
    protected readonly ClassEditorViewModel ClassEditor;
    protected readonly NodeGraphViewModel Graph;

    protected static readonly TypeSpecifier StringType = TypeSpecifier.FromType<string>();
    protected static readonly TypeSpecifier IntType = TypeSpecifier.FromType<int>();

    protected GraphTestBase(TestEditor editor)
    {
        Editor = editor;
        Class = new ClassGraph { Name = "C", Namespace = "N" };
        ClassEditor = new ClassEditorViewModel(Class, Editor.Context);
        ClassEditor.CreateMethodCommand.Execute(null);
        Method = (MethodGraph)ClassEditor.Methods.Single().Graph;
        Graph = ClassEditor.OpenedGraph!;
    }

    public void Dispose()
    {
        ClassEditor.Dispose();
        GC.SuppressFinalize(this);
    }

    protected NodeViewModel VmOf(Node node) => Graph.Nodes.Single(n => n.Node == node);

    protected NodePinViewModel VmOf(NodePin pin) => Graph.Nodes.SelectMany(n => n.AllPins).Single(p => p.Pin == pin);

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
