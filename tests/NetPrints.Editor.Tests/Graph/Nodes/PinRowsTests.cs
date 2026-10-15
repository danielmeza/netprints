using NetPrints.Editor.Tests.Graph;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Graph.Nodes;

/// <summary>
/// NodeViewModel.PinRows pairing (OWN-05b, owner-reported): a parameter's or return value's type and data
/// pin must share a row no matter how many other pins (Exec, generic type parameters) sit around
/// them; every other node kind keeps pairing Inputs[i] with Outputs[i].
/// </summary>
public class PinRowsTests(TestEditor editor) : GraphTestBase(editor)
{
    [Fact]
    public void MethodEntryRowsPairTypeAndDataPinsPerParameter()
    {
        var entry = VmOf(Method.EntryNode);

        // 0 parameters: just the Exec row, nothing on the left.
        Assert.Single(entry.PinRows);
        Assert.Null(entry.PinRows[0].Left);
        Assert.Equal("Exec", Require(entry.PinRows[0].Right).Pin.Name);

        // 1 parameter: row 1 pairs its type pin with its data pin.
        Method.MethodEntryNode.AddArgument();
        Assert.Equal(2, entry.PinRows.Count);
        Assert.Equal(Method.MethodEntryNode.InputTypePins[0], Require(entry.PinRows[1].Left).Pin);
        Assert.Equal(Method.MethodEntryNode.OutputDataPins[0], Require(entry.PinRows[1].Right).Pin);

        // 3 parameters: row i+1 pairs InputTypePins[i] with OutputDataPins[i] for every i.
        Method.MethodEntryNode.AddArgument();
        Method.MethodEntryNode.AddArgument();
        Assert.Equal(4, entry.PinRows.Count);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(Method.MethodEntryNode.InputTypePins[i], Require(entry.PinRows[i + 1].Left).Pin);
            Assert.Equal(Method.MethodEntryNode.OutputDataPins[i], Require(entry.PinRows[i + 1].Right).Pin);
        }
    }

    [Fact]
    public void MethodEntryRowsUpdateWhenAParameterIsRemoved()
    {
        var entry = VmOf(Method.EntryNode);
        Method.MethodEntryNode.AddArgument();
        Method.MethodEntryNode.AddArgument();

        Method.MethodEntryNode.RemoveArgument();

        Assert.Equal(2, entry.PinRows.Count);
        Assert.Equal(Method.MethodEntryNode.InputTypePins[0], Require(entry.PinRows[1].Left).Pin);
        Assert.Equal(Method.MethodEntryNode.OutputDataPins[0], Require(entry.PinRows[1].Right).Pin);
    }

    [Fact]
    public void MethodEntryGenericTypeParametersGetTheirOwnRow()
    {
        var entry = VmOf(Method.EntryNode);
        Method.MethodEntryNode.AddArgument();
        Method.MethodEntryNode.AddGenericArgument();

        // Row 0 = Exec, row 1 = the parameter, row 2 = the generic type parameter (right only).
        Assert.Equal(3, entry.PinRows.Count);
        Assert.Null(entry.PinRows[2].Left);
        Assert.Equal(Method.MethodEntryNode.OutputTypePins[0], Require(entry.PinRows[2].Right).Pin);
    }

    [Fact]
    public void ReturnNodeRowsPairDataAndTypePinsPerReturnValue()
    {
        var ret = VmOf(Method.MainReturnNode);

        // 0 return values: just the return Exec row, nothing on the right.
        Assert.Single(ret.PinRows);
        Assert.Equal("Exec", Require(ret.PinRows[0].Left).Pin.Name);
        Assert.Null(ret.PinRows[0].Right);

        Method.MainReturnNode.AddReturnType();
        Method.MainReturnNode.AddReturnType();

        Assert.Equal(3, ret.PinRows.Count);
        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(Method.MainReturnNode.InputDataPins[i], Require(ret.PinRows[i + 1].Left).Pin);
            Assert.Equal(Method.MainReturnNode.InputTypePins[i], Require(ret.PinRows[i + 1].Right).Pin);
        }
    }

    [Fact]
    public void OtherNodesPairInputsAndOutputsByIndex()
    {
        var call = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));

        Assert.Equal(Math.Max(call.Inputs.Count, call.Outputs.Count), call.PinRows.Count);
        for (int i = 0; i < call.PinRows.Count; i++)
        {
            Assert.Equal(i < call.Inputs.Count ? call.Inputs[i] : null, call.PinRows[i].Left);
            Assert.Equal(i < call.Outputs.Count ? call.Outputs[i] : null, call.PinRows[i].Right);
        }
    }

    /// <summary>Unwraps a row's optional pin slot, failing with a clear message instead of a bare null-reference when the row shape a test assumes doesn't hold.</summary>
    private static T Require<T>(T? value) where T : class =>
        value ?? throw new InvalidOperationException("Expected this pin row to have a pin.");
}
