using System.Runtime.CompilerServices;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Editor.UndoRedo;

/// <summary>
/// Undoable editor commands (replaces the WPF <c>NetPrintsCommands</c> routed commands and their
/// <c>MakeUndoCommand</c> table). Undo pairs follow PAR-60.
/// </summary>
public static class EditorCommands
{
    /// <summary>Adds a variable of type <c>object</c>; undo removes it.</summary>
    public static IUndoableCommand AddVariable(ClassGraph cls, string name)
    {
        Variable? variable = null;
        return new DelegateUndoableCommand("Add variable",
            () =>
            {
                variable ??= new Variable(cls, name, TypeSpecifier.FromType<object>(), null, null, VariableModifiers.None);
                cls.Variables.Add(variable);
            },
            () => cls.Variables.Remove(variable!));
    }

    /// <summary>Removes a variable; undo restores the same variable at its position.</summary>
    public static IUndoableCommand RemoveVariable(ClassGraph cls, Variable variable)
    {
        int index = -1;
        return new DelegateUndoableCommand("Remove variable",
            () =>
            {
                index = cls.Variables.IndexOf(variable);
                cls.Variables.Remove(variable);
            },
            () => cls.Variables.Insert(Math.Clamp(index, 0, cls.Variables.Count), variable));
    }

    /// <summary>Adds a getter; undo removes it (redo restores the same getter).</summary>
    public static IUndoableCommand AddGetter(Variable variable)
    {
        MethodGraph? getter = null;
        return new DelegateUndoableCommand("Add getter",
            () => variable.GetterMethod = getter ??= ModelOperations.CreateGetter(variable),
            () => variable.GetterMethod = null);
    }

    /// <summary>Removes the getter; undo restores it.</summary>
    public static IUndoableCommand RemoveGetter(Variable variable)
    {
        MethodGraph? old = null;
        return new DelegateUndoableCommand("Remove getter",
            () =>
            {
                old = variable.GetterMethod;
                variable.GetterMethod = null;
            },
            () => variable.GetterMethod = old);
    }

    /// <summary>Adds a setter; undo removes it (redo restores the same setter).</summary>
    public static IUndoableCommand AddSetter(Variable variable)
    {
        MethodGraph? setter = null;
        return new DelegateUndoableCommand("Add setter",
            () => variable.SetterMethod = setter ??= ModelOperations.CreateSetter(variable),
            () => variable.SetterMethod = null);
    }

    /// <summary>Removes the setter; undo restores it.</summary>
    public static IUndoableCommand RemoveSetter(Variable variable)
    {
        MethodGraph? old = null;
        return new DelegateUndoableCommand("Remove setter",
            () =>
            {
                old = variable.SetterMethod;
                variable.SetterMethod = null;
            },
            () => variable.SetterMethod = old);
    }

    /// <summary>
    /// A node that survives overload changes. Changing the overload of a call or constructor node
    /// replaces the node, so every overload command of that node goes through one shared handle
    /// that follows the replacements; otherwise undoing two changes in a row would act on a node
    /// that is no longer in the graph.
    /// </summary>
    private sealed class NodeHandle(Node node)
    {
        public Node Node { get; set; } = node;
    }

    private static readonly ConditionalWeakTable<Node, NodeHandle> NodeHandles = [];

    /// <summary>Changes the overload of a node; undo restores the previous overload (PAR-40, PAR-60).</summary>
    public static IUndoableCommand ChangeOverload(Node node, object newOverload)
    {
        var handle = NodeHandles.GetValue(node, n => new NodeHandle(n));
        object? previous = null;

        void Apply(object overload)
        {
            var replacement = ModelOperations.ChangeOverload(handle.Node, overload);
            NodeHandles.AddOrUpdate(replacement, handle);
            handle.Node = replacement;
        }

        return new DelegateUndoableCommand("Change overload",
            () =>
            {
                previous = ModelOperations.GetCurrentOverload(handle.Node);
                Apply(newOverload);
            },
            () =>
            {
                if (previous is not null)
                {
                    Apply(previous);
                }
            });
    }

    /// <summary>Removes a method or constructor. As in the WPF editor, undo does nothing.</summary>
    public static IUndoableCommand RemoveMethod(ClassGraph cls, ExecutionGraph graph)
    {
        return new DelegateUndoableCommand("Remove method",
            () =>
            {
                if (graph is MethodGraph method)
                {
                    cls.Methods.Remove(method);
                }
                else if (graph is ConstructorGraph constructor)
                {
                    cls.Constructors.Remove(constructor);
                }
            },
            () => { });
    }
}
