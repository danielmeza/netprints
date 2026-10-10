using NetPrints.Core;
using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.UndoRedo;

/// <summary>Deleting a node restores its type wires on undo, so a typed method parameter keeps its type.</summary>
public sealed class DeleteTypeNodeUndoTests(TestEditor editor) : GraphTestBase(editor)
{
    [Fact]
    public void UndoingTheDeleteOfTheTypeNodeOfAParameterRestoresTheTypeWire()
    {
        var type = new TypeNode(Method, IntType);
        MethodEntryNode entry = Method.MethodEntryNode;
        entry.AddArgument();
        GraphUtil.ConnectTypePins(type.OutputTypePins[0], entry.InputTypePins[0]);
        ClassContext.UndoRedo.Clear();
        Assert.Equal(IntType, entry.OutputDataPins[0].PinType.Value);
        Graph.SelectNodes([VmOf(type)], deselectPrevious: true);

        Graph.DeleteSelectedNodes();
        Assert.Equal(TypeSpecifier.FromType<object>(), entry.OutputDataPins[0].PinType.Value);

        ClassContext.UndoRedo.Undo();
        Assert.Same(type.OutputTypePins[0], entry.InputTypePins[0].IncomingPin);
        Assert.Equal(IntType, entry.OutputDataPins[0].PinType.Value);

        ClassContext.UndoRedo.Redo();
        Assert.Null(entry.InputTypePins[0].IncomingPin);
        Assert.Equal(TypeSpecifier.FromType<object>(), entry.OutputDataPins[0].PinType.Value);
    }
}
