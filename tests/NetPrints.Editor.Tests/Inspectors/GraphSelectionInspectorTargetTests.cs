using NetPrints.Core;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.UndoRedo;
using NetPrints.Graph;

namespace NetPrints.Editor.Tests.Inspectors;

public sealed class GraphSelectionInspectorTargetTests
{
    private static ClassGraph NewClass(out Project project)
    {
        project = Project.FromSnapshot(TestSnapshots.Empty("P", "N"));
        var cls = new ClassGraph { Name = "C", Namespace = "N", Project = project };
        project.Classes.Add(cls);
        return cls;
    }

    [Fact]
    public void ASeveralNodeSelectionShowsTheGraphsOwner()
    {
        ClassGraph cls = NewClass(out _);
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));
        var method = new MethodGraph("Run") { Class = cls };
        cls.Methods.Add(method);
        var getter = new VariableGetterNode(method, cls.Variables[0].Specifier);
        var setter = new VariableSetterNode(method, cls.Variables[0].Specifier);

        Assert.Same(method, GraphSelectionInspectorTarget.Resolve(method, [getter, setter]));
        Assert.Same(method, GraphSelectionInspectorTarget.Resolve(method, []));
    }

    [Fact]
    public void ASetterNodeShowsItsVariable()
    {
        ClassGraph cls = NewClass(out _);
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));
        var method = new MethodGraph("Run") { Class = cls };
        cls.Methods.Add(method);

        Assert.Same(cls.Variables[0], GraphSelectionInspectorTarget.Resolve(method, [new VariableSetterNode(method, cls.Variables[0].Specifier)]));
    }

    [Fact]
    public void ANodeOfAVariableTheProjectDoesNotHaveShowsTheGraphsOwner()
    {
        ClassGraph cls = NewClass(out _);
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));
        var method = new MethodGraph("Run") { Class = cls };
        cls.Methods.Add(method);
        VariableSpecifier foreign = new("Elsewhere", TypeSpecifier.FromType<int>(), MemberVisibility.Public, MemberVisibility.Public, TypeSpecifier.FromType<string>(), VariableModifiers.None);

        Assert.Same(method, GraphSelectionInspectorTarget.Resolve(method, [new VariableGetterNode(method, foreign)]));
    }

    [Fact]
    public void AnAccessorOrTypeGraphIsOwnedByItsVariable()
    {
        ClassGraph cls = NewClass(out _);
        new UndoRedoStack().Do(EditorCommands.AddVariable(cls, "count"));
        Variable variable = cls.Variables[0];

        Assert.Same(variable, GraphSelectionInspectorTarget.Resolve(variable.TypeGraph, []));
        var getter = new MethodGraph("get_count") { Class = cls };
        variable.GetterMethod = getter;
        Assert.Same(variable, GraphSelectionInspectorTarget.Resolve(getter, []));
    }
}
