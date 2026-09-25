using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.Graph.Pins;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Graph;

namespace NetPrints.Editor.Tests.Graph.Pins;

public class NodePinVMTests(TestEditor editor) : GraphTestBase(editor)
{
    [Fact]
    public void KindShapeAndDirection()
    {
        var entry = VmOf(Method.EntryNode);
        var exec = entry.OutputExecPins.Single();
        Assert.Equal(PinKind.Exec, exec.Kind);
        Assert.True(exec.ShowRectangle);
        Assert.True(exec.IsOutput);

        var call = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        var data = call.InputDataPins.Single();
        Assert.Equal(PinKind.Data, data.Kind);
        Assert.True(data.ShowCircle);
        Assert.True(data.IsInput);

        var typeNode = VmOf(new TypeNode(Method, IntType));
        var type = typeNode.OutputTypePins.Single();
        Assert.Equal(PinKind.Type, type.Kind);
        Assert.True(type.ShowTriangle);
    }

    [Fact]
    public void UnconnectedPinsAreDimmedAndConnectedAreNot()
    {
        var entry = VmOf(Method.EntryNode).OutputExecPins.Single();
        Assert.True(entry.IsConnected, "entry and return are connected when a method is created");
        Assert.False(entry.IsDimmed);

        var call = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        Assert.True(call.InputExecPins.Single().IsDimmed);
    }

    [Fact]
    public void UnconnectedEditorsByType()
    {
        var textPin = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType))).InputDataPins.Single();
        Assert.True(textPin.ShowUnconnectedValue);
        Assert.False(textPin.ShowBooleanValue);
        textPin.UnconnectedText = "hello";
        Assert.Equal("hello", ((NodeInputDataPin)textPin.Pin).UnconnectedValue);

        var intPin = VmOf(new CallMethodNode(Method, ConsoleWriteLine(IntType))).InputDataPins.Single();
        intPin.UnconnectedText = "42";
        Assert.Equal(42, ((NodeInputDataPin)intPin.Pin).UnconnectedValue);
        intPin.UnconnectedText = "not a number";
        Assert.Equal(42, ((NodeInputDataPin)intPin.Pin).UnconnectedValue);

        var boolPin = VmOf(new CallMethodNode(Method, ConsoleWriteLine(TypeSpecifier.FromType<bool>()))).InputDataPins.Single();
        Assert.True(boolPin.ShowBooleanValue);
        Assert.False(boolPin.ShowUnconnectedValue);
        boolPin.UnconnectedBool = true;
        Assert.Equal(true, ((NodeInputDataPin)boolPin.Pin).UnconnectedValue);

        var enumType = TypeSpecifier.FromType<DayOfWeek>();
        Assert.True(enumType.IsEnum);
        var enumPin = VmOf(new CallMethodNode(Method, ConsoleWriteLine(enumType))).InputDataPins.Single();
        Assert.True(enumPin.ShowEnumValue);
        Assert.Contains("Monday", enumPin.PossibleEnumNames!.ToList());
        enumPin.UnconnectedEnumName = "Monday";
        Assert.Equal("Monday", ((NodeInputDataPin)enumPin.Pin).UnconnectedValue);

        // Middle click clears the value.
        textPin.ClearUnconnectedValue();
        Assert.Null(((NodeInputDataPin)textPin.Pin).UnconnectedValue);
    }

    [Fact]
    public void DefaultValueIndicatorAndWatermark()
    {
        var method = new MethodSpecifier("M",
            [new MethodParameter("x", IntType, MethodParameterPassType.Default, true, 5)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Math)), []);
        var pin = VmOf(new CallMethodNode(Method, method)).InputDataPins.Single();

        Assert.True(pin.ShowDefaultValueIndicator);
        Assert.True(pin.IsDefaultValueActive);
        Assert.Equal("5", pin.UnconnectedTextWatermark);
        Assert.Contains("Default: 5", pin.ToolTip);

        pin.UnconnectedText = "7";
        Assert.False(pin.IsDefaultValueActive, "an explicit value overrides the default");
        Assert.Null(pin.UnconnectedTextWatermark);
    }

    [Fact]
    public void NameEditableOnlyForEntryOutputsAndReturnInputs()
    {
        Method.MethodEntryNode.AddArgument();
        Method.MainReturnNode.AddReturnType();

        var argument = VmOf(Method.EntryNode).OutputDataPins.Single();
        Assert.True(argument.IsNameEditable);
        argument.Name = "count";
        Assert.Equal("count", argument.Pin.Name);

        Assert.True(VmOf(Method.MainReturnNode).InputDataPins.Single().IsNameEditable);
        var call = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        Assert.False(call.InputDataPins.Single().IsNameEditable);
    }

    [Fact]
    public void ConnectToChecksTypes()
    {
        var toUpper = FindMethod(typeof(string), "ToUpperInvariant");
        var upper = VmOf(new CallMethodNode(Method, toUpper) { IsPure = true });
        var writeString = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        var writeObject = VmOf(new CallMethodNode(Method, ConsoleWriteLine(TypeSpecifier.FromType<object>())));
        var writeLong = VmOf(new CallMethodNode(Method, ConsoleWriteLine(TypeSpecifier.FromType<long>())));
        var writeInt = VmOf(new CallMethodNode(Method, ConsoleWriteLine(IntType)));
        var length = VmOf(new VariableGetterNode(Method, new VariableSpecifier("Length", IntType, MemberVisibility.Public, MemberVisibility.Public, StringType, VariableModifiers.None)));

        var stringOut = upper.OutputDataPins.Single(p => p.Kind == PinKind.Data && p.Pin.Name != "Exception");
        var intOut = length.OutputDataPins.Single();

        Assert.False(intOut.ConnectTo(writeString.InputDataPins.Single()), "int does not go into string");
        Assert.False(writeString.InputDataPins.Single().IsConnected);

        Assert.True(stringOut.ConnectTo(writeString.InputDataPins.Single()));
        Assert.True(writeObject.InputDataPins.Single().ConnectTo(stringOut), "subclass rule: string is an object; order does not matter");
        Assert.True(intOut.ConnectTo(writeLong.InputDataPins.Single()), "implicit cast int -> long");
        Assert.True(intOut.ConnectTo(writeInt.InputDataPins.Single()));

        var entryExec = VmOf(Method.EntryNode).OutputExecPins.Single();
        Assert.False(entryExec.ConnectTo(writeString.InputDataPins.Single()), "exec does not connect to data");
        Assert.True(entryExec.ConnectTo(writeString.InputExecPins.Single()));
        Assert.True(writeString.InputExecPins.Single().IsConnected);
    }

    [Fact]
    public void DisconnectAllAndReroute()
    {
        var entryExec = VmOf(Method.EntryNode).OutputExecPins.Single();
        var returnExec = VmOf(Method.MainReturnNode).InputExecPins.Single();
        Method.EntryNode.PositionX = 0;
        Method.MainReturnNode.PositionX = 280;
        Method.EntryNode.PositionY = 0;
        Method.MainReturnNode.PositionY = 56;

        entryExec.AddRerouteNode();
        var reroute = Method.Nodes.OfType<RerouteNode>().Single();
        Assert.Equal(140, reroute.PositionX);
        Assert.Equal(28, reroute.PositionY);
        Assert.Same(reroute.InputExecPins[0], Method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.Same(Method.MainReturnNode.ReturnPin, reroute.OutputExecPins[0].OutgoingPin);

        VmOf(reroute).InputExecPins.Single().DisconnectAll();
        Assert.Null(Method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.False(entryExec.IsConnected);
        Assert.True(returnExec.IsConnected);
    }

    [Fact]
    public void ToggleFaintAffectsCables()
    {
        var entryExec = VmOf(Method.EntryNode).OutputExecPins.Single();
        var connection = Graph.Connections.Single();
        Assert.False(connection.IsFaint);

        entryExec.ToggleFaint();
        Assert.True(connection.IsFaint);
        connection.ToggleFaint();
        Assert.False(connection.IsFaint);
    }
}
