using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.ProjectTree;
using NetPrints.Editor.UndoRedo;
using NetPrints.Editor.Variables;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Inspectors;

/// <summary>Renaming a method or a variable in the inspector rewrites the nodes that use it, as one undo step.</summary>
public sealed class MemberRenameTests : IAsyncDisposable
{
    private readonly ShellPanelRig rig = new();

    public ValueTask DisposeAsync() => rig.DisposeAsync();

    private static MethodGraph AddMethod(ClassGraph cls, string name, params BaseType[] parameters)
    {
        var method = new MethodGraph(name) { Class = cls };
        foreach (BaseType parameter in parameters)
        {
            method.MethodEntryNode.AddArgument();
            method.MethodEntryNode.OutputDataPins[^1].PinType.Value = parameter;
        }

        cls.Methods.Add(method);
        return method;
    }

    private static CallMethodNode CallTo(NodeGraph caller, ClassContext owner, MethodGraph callee) =>
        new(caller, owner.Methods.Single(item => ReferenceEquals(item.Graph, callee)).ToMethodSpecifier(owner.Class.Type));

    [Fact]
    public async Task RenamingAMethodUpdatesTheCallsToIt()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        MethodGraph main = cls.Methods[0];
        MethodGraph callee = AddMethod(cls, "Callee");
        ClassContext context = session.ContextFor(cls);
        CallMethodNode call = CallTo(main, context, callee);

        context.Methods.Single(item => ReferenceEquals(item.Graph, callee)).Name = "Renamed";

        Assert.Equal("Renamed", callee.Name);
        Assert.Equal("Renamed", call.MethodName);
    }

    [Fact]
    public async Task RenamingAMethodIsOneUndoStepAndRedoes()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        MethodGraph callee = AddMethod(cls, "Callee");
        ClassContext context = session.ContextFor(cls);
        CallMethodNode first = CallTo(cls.Methods[0], context, callee);
        CallMethodNode second = CallTo(callee, context, callee);
        context.Methods.Single(item => ReferenceEquals(item.Graph, callee)).Name = "Renamed";

        Assert.True(context.UndoRedo.Undo());

        Assert.Equal("Callee", callee.Name);
        Assert.Equal("Callee", first.MethodName);
        Assert.Equal("Callee", second.MethodName);

        Assert.True(context.UndoRedo.Redo());

        Assert.Equal("Renamed", callee.Name);
        Assert.Equal("Renamed", first.MethodName);
        Assert.Equal("Renamed", second.MethodName);
    }

    [Fact]
    public async Task RenamingAMethodKeepsOverloadsAndOtherClassesApart()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        var other = new ClassGraph { Name = "Other", Namespace = cls.Namespace, Project = session.Project };
        session.Project.Classes.Add(other);
        MethodGraph plain = AddMethod(cls, "Callee");
        MethodGraph overload = AddMethod(cls, "Callee", TypeSpecifier.FromType<int>());
        MethodGraph foreign = AddMethod(other, "Callee");
        ClassContext context = session.ContextFor(cls);
        CallMethodNode toPlain = CallTo(cls.Methods[0], context, plain);
        CallMethodNode toOverload = CallTo(cls.Methods[0], context, overload);
        CallMethodNode toForeign = CallTo(cls.Methods[0], session.ContextFor(other), foreign);
        CallMethodNode fromOther = CallTo(foreign, context, plain);

        context.Methods.Single(item => ReferenceEquals(item.Graph, plain)).Name = "Renamed";

        Assert.Equal("Renamed", toPlain.MethodName);
        Assert.Equal("Renamed", fromOther.MethodName);
        Assert.Equal("Callee", toOverload.MethodName);
        Assert.Equal("Callee", toForeign.MethodName);
    }

    [Fact]
    public async Task RenamingAVariableUpdatesItsGettersAndSetters()
    {
        ProjectSessionViewModel session = await rig.OpenSessionAsync();
        ClassGraph cls = session.Project.Classes[0];
        MethodGraph main = cls.Methods[0];
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "other"));
        Variable variable = cls.Variables[0];
        var getter = new VariableGetterNode(main, variable.Specifier);
        var setter = new VariableSetterNode(main, variable.Specifier);
        var untouched = new VariableGetterNode(main, cls.Variables[1].Specifier);
        ClassContext context = session.ContextFor(cls);
        MemberVariableViewModel entry = context.Variables.Single(item => ReferenceEquals(item.Variable, variable));

        entry.Name = "total";

        Assert.Equal("total", variable.Name);
        Assert.Equal("total", getter.VariableName);
        Assert.Equal("total", setter.VariableName);
        Assert.Equal("other", untouched.VariableName);

        context.UndoRedo.Undo();
        Assert.Equal("count", variable.Name);
        Assert.Equal("count", getter.VariableName);
        Assert.Equal("count", setter.VariableName);

        context.UndoRedo.Redo();
        Assert.Equal("total", getter.VariableName);
    }
}
