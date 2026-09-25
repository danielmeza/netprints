using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Editor.ViewModels;

namespace NetPrints.Editor.Tests.ViewModels;

[TestClass]
public class NodePinVMTests : GraphTestBase
{
    [TestMethod]
    public void KindShapeAndDirection()
    {
        var entry = VmOf(Method.EntryNode);
        var exec = entry.OutputExecPins.Single();
        Assert.AreEqual(PinKind.Exec, exec.Kind);
        Assert.IsTrue(exec.ShowRectangle);
        Assert.IsTrue(exec.IsOutput);

        var call = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        var data = call.InputDataPins.Single();
        Assert.AreEqual(PinKind.Data, data.Kind);
        Assert.IsTrue(data.ShowCircle);
        Assert.IsTrue(data.IsInput);

        var typeNode = VmOf(new TypeNode(Method, IntType));
        var type = typeNode.OutputTypePins.Single();
        Assert.AreEqual(PinKind.Type, type.Kind);
        Assert.IsTrue(type.ShowTriangle);
    }

    [TestMethod]
    public void UnconnectedPinsAreDimmedAndConnectedAreNot()
    {
        var entry = VmOf(Method.EntryNode).OutputExecPins.Single();
        Assert.IsTrue(entry.IsConnected, "entry and return are connected when a method is created");
        Assert.IsFalse(entry.IsDimmed);

        var call = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        Assert.IsTrue(call.InputExecPins.Single().IsDimmed);
    }

    [TestMethod]
    public void UnconnectedEditorsByType()
    {
        var textPin = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType))).InputDataPins.Single();
        Assert.IsTrue(textPin.ShowUnconnectedValue);
        Assert.IsFalse(textPin.ShowBooleanValue);
        textPin.UnconnectedText = "hello";
        Assert.AreEqual("hello", ((NodeInputDataPin)textPin.Pin).UnconnectedValue);

        var intPin = VmOf(new CallMethodNode(Method, ConsoleWriteLine(IntType))).InputDataPins.Single();
        intPin.UnconnectedText = "42";
        Assert.AreEqual(42, ((NodeInputDataPin)intPin.Pin).UnconnectedValue, "text is converted to the pin type");
        intPin.UnconnectedText = "not a number";
        Assert.AreEqual(42, ((NodeInputDataPin)intPin.Pin).UnconnectedValue, "invalid text is ignored");

        var boolPin = VmOf(new CallMethodNode(Method, ConsoleWriteLine(TypeSpecifier.FromType<bool>()))).InputDataPins.Single();
        Assert.IsTrue(boolPin.ShowBooleanValue);
        Assert.IsFalse(boolPin.ShowUnconnectedValue);
        boolPin.UnconnectedBool = true;
        Assert.AreEqual(true, ((NodeInputDataPin)boolPin.Pin).UnconnectedValue);

        var enumType = TypeSpecifier.FromType<DayOfWeek>();
        Assert.IsTrue(enumType.IsEnum);
        var enumPin = VmOf(new CallMethodNode(Method, ConsoleWriteLine(enumType))).InputDataPins.Single();
        Assert.IsTrue(enumPin.ShowEnumValue);
        CollectionAssert.Contains(enumPin.PossibleEnumNames!.ToList(), "Monday");
        enumPin.UnconnectedEnumName = "Monday";
        Assert.AreEqual("Monday", ((NodeInputDataPin)enumPin.Pin).UnconnectedValue);

        // Middle click clears the value.
        textPin.ClearUnconnectedValue();
        Assert.IsNull(((NodeInputDataPin)textPin.Pin).UnconnectedValue);
    }

    [TestMethod]
    public void DefaultValueIndicatorAndWatermark()
    {
        var method = new MethodSpecifier("M",
            [new MethodParameter("x", IntType, MethodParameterPassType.Default, true, 5)],
            [], MethodModifiers.Static, MemberVisibility.Public, TypeSpecifier.FromType(typeof(Math)), []);
        var pin = VmOf(new CallMethodNode(Method, method)).InputDataPins.Single();

        Assert.IsTrue(pin.ShowDefaultValueIndicator);
        Assert.IsTrue(pin.IsDefaultValueActive);
        Assert.AreEqual("5", pin.UnconnectedTextWatermark);
        StringAssert.Contains(pin.ToolTip, "Default: 5");

        pin.UnconnectedText = "7";
        Assert.IsFalse(pin.IsDefaultValueActive, "an explicit value overrides the default");
        Assert.IsNull(pin.UnconnectedTextWatermark);
    }

    [TestMethod]
    public void NameEditableOnlyForEntryOutputsAndReturnInputs()
    {
        Method.MethodEntryNode.AddArgument();
        Method.MainReturnNode.AddReturnType();

        var argument = VmOf(Method.EntryNode).OutputDataPins.Single();
        Assert.IsTrue(argument.IsNameEditable);
        argument.Name = "count";
        Assert.AreEqual("count", argument.Pin.Name);

        Assert.IsTrue(VmOf(Method.MainReturnNode).InputDataPins.Single().IsNameEditable);
        var call = VmOf(new CallMethodNode(Method, ConsoleWriteLine(StringType)));
        Assert.IsFalse(call.InputDataPins.Single().IsNameEditable);
    }

    [TestMethod]
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

        Assert.IsFalse(intOut.ConnectTo(writeString.InputDataPins.Single()), "int does not go into string");
        Assert.IsFalse(writeString.InputDataPins.Single().IsConnected);

        Assert.IsTrue(stringOut.ConnectTo(writeString.InputDataPins.Single()));
        Assert.IsTrue(writeObject.InputDataPins.Single().ConnectTo(stringOut), "subclass rule: string is an object; order does not matter");
        Assert.IsTrue(intOut.ConnectTo(writeLong.InputDataPins.Single()), "implicit cast int -> long");
        Assert.IsTrue(intOut.ConnectTo(writeInt.InputDataPins.Single()));

        var entryExec = VmOf(Method.EntryNode).OutputExecPins.Single();
        Assert.IsFalse(entryExec.ConnectTo(writeString.InputDataPins.Single()), "exec does not connect to data");
        Assert.IsTrue(entryExec.ConnectTo(writeString.InputExecPins.Single()));
        Assert.IsTrue(writeString.InputExecPins.Single().IsConnected);
    }

    [TestMethod]
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
        Assert.AreEqual(140, reroute.PositionX, "reroute is placed midway");
        Assert.AreEqual(28, reroute.PositionY);
        Assert.AreSame(reroute.InputExecPins[0], Method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.AreSame(Method.MainReturnNode.ReturnPin, reroute.OutputExecPins[0].OutgoingPin);

        VmOf(reroute).InputExecPins.Single().DisconnectAll();
        Assert.IsNull(Method.EntryNode.InitialExecutionPin.OutgoingPin);
        Assert.IsFalse(entryExec.IsConnected);
        Assert.IsTrue(returnExec.IsConnected);
    }

    [TestMethod]
    public void ToggleFaintAffectsCables()
    {
        var entryExec = VmOf(Method.EntryNode).OutputExecPins.Single();
        var connection = Graph.Connections.Single();
        Assert.IsFalse(connection.IsFaint);

        entryExec.ToggleFaint();
        Assert.IsTrue(connection.IsFaint);
        connection.ToggleFaint();
        Assert.IsFalse(connection.IsFaint);
    }
}
