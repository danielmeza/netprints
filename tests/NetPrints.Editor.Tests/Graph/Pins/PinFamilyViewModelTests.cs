using NetPrints.Core;
using NetPrints.Editor.Commands;
using NetPrints.Editor.Graph.Nodes;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Graph.Pins;

/// <summary>The family class of a pin and of its wire follow the pin's type (FR-107, T092n).</summary>
public class PinFamilyViewModelTests(TestEditor editor) : GraphTestBase(editor)
{
    private static readonly TypeSpecifier BoolType = TypeSpecifier.FromType<bool>();

    private async Task DeleteAsync(NodeViewModel node)
    {
        Graph.SelectNodes([node], deselectPrevious: true);
        var context = new CommandContext(new FakeShell(), null, null, Graph, new CommandSelection([.. Graph.SelectedNodes]));
        await new DeleteCommandHandler().ExecuteAsync(context, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void EachPinKindTakesItsFamilyAndStyleClass()
    {
        var call = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        var typeNode = VmOf(new TypeNode(Method, IntType));

        Assert.Same(PinTypeFamily.Exec, call.InputExecPins.Single().Family);
        Assert.Equal("pin-exec", call.InputExecPins.Single().FamilyClass);
        Assert.Same(PinTypeFamily.String, call.InputDataPins.Single().Family);
        Assert.Equal("pin-string", call.InputDataPins.Single().FamilyClass);
        Assert.Same(PinTypeFamily.Type, typeNode.OutputTypePins.Single().Family);
        Assert.Equal("pin-type", typeNode.OutputTypePins.Single().FamilyClass);
    }

    [Fact]
    public void AnOverloadChangeAndItsUndoMoveThePinToTheOtherFamily()
    {
        var call = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        Assert.Same(PinTypeFamily.String, call.InputDataPins.Single().Family);
        ClassContext.UndoRedo.Clear();

        Graph.ChangeOverload(call, ConsoleWriteLine(BoolType));
        var changed = Graph.Nodes.Single(n => n.Node is CallMethodNode);
        Assert.Same(PinTypeFamily.Bool, changed.InputDataPins.Single().Family);

        ClassContext.UndoRedo.Undo();
        var restored = Graph.Nodes.Single(n => n.Node is CallMethodNode);
        Assert.Same(PinTypeFamily.String, restored.InputDataPins.Single().Family);
    }

    [Fact]
    public void AGenericPinTakesTheFamilyOfItsResolvedType()
    {
        var ternary = new TernaryNode(Method);
        var typeNode = new TypeNode(Method, StringType);
        var output = VmOf(ternary.OutputObjectPin);
        var changed = new List<string?>();
        output.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        Assert.True(Graph.Connect(VmOf(typeNode.OutputTypePins[0]), VmOf(ternary.TypePin)));

        Assert.Same(PinTypeFamily.String, output.Family);
        Assert.Contains(nameof(NodePinViewModel.FamilyClass), changed);
        Assert.Same(PinTypeFamily.String, VmOf(ternary.TrueObjectPin).Family);
    }

    [Fact]
    public async Task ARetypedParameterFollowsItsTypeWireAndDeletingTheTypeNode()
    {
        Method.MethodEntryNode.AddArgument();
        var parameter = VmOf(Method.MethodEntryNode.OutputDataPins.Single());
        var stringType = new TypeNode(Method, StringType);
        Assert.Same(PinTypeFamily.Object, parameter.Family);

        Assert.True(Graph.Connect(VmOf(stringType.OutputTypePins[0]), VmOf(Method.MethodEntryNode.InputTypePins[0])));
        Assert.Same(PinTypeFamily.String, parameter.Family);

        ClassContext.UndoRedo.Clear();
        await DeleteAsync(VmOf(stringType));
        Assert.Same(PinTypeFamily.Object, parameter.Family);

    }

    [Fact]
    public void AWireTakesTheFamilyOfItsSourcePinAndFollowsItsType()
    {
        Method.MethodEntryNode.AddArgument();
        var typeNode = new TypeNode(Method, StringType);
        Graph.Connect(VmOf(typeNode.OutputTypePins[0]), VmOf(Method.MethodEntryNode.InputTypePins[0]));
        var call = new CallMethodNode(Method, ConsoleWriteLine(StringType));
        var source = VmOf(Method.MethodEntryNode.OutputDataPins.Single());
        Assert.True(Graph.Connect(source, VmOf(call.InputDataPins[0])));

        var wire = Graph.Connections.Single(c => c.Source == source);
        var changed = new List<string?>();
        wire.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        Assert.Equal("pin-string", wire.KindClass);

        var intType = new TypeNode(Method, IntType);
        Graph.Connect(VmOf(intType.OutputTypePins[0]), VmOf(Method.MethodEntryNode.InputTypePins[0]));

        Assert.Equal("pin-integer", wire.KindClass);
        Assert.Contains(nameof(ConnectionViewModel.KindClass), changed);
    }

    [Fact]
    public void AnExecutionWireIsExecAndATypeWireIsType()
    {
        var typeNode = new TypeNode(Method, StringType);
        Method.MethodEntryNode.AddArgument();
        Graph.Connect(VmOf(typeNode.OutputTypePins[0]), VmOf(Method.MethodEntryNode.InputTypePins[0]));

        Assert.Equal("pin-exec", Graph.Connections.First(c => c.Source.Kind == PinKind.Exec).KindClass);
        Assert.Equal("pin-type", Graph.Connections.Single(c => c.Source.Kind == PinKind.Type).KindClass);
    }
}
