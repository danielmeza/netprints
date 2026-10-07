#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Graph;

namespace NetPrints.Core;

/// <summary>
/// Renames a method or a member variable and every node of the project that points at it. Nodes keep a
/// specifier copy of the member they use, so a plain name change leaves them on the old name.
/// </summary>
/// <remarks>
/// Nodes are matched by the member's identity (declaring type, and for a method its parameter types), never
/// by name alone, so overloads and same-named members of other classes stay apart.
/// </remarks>
public static class MemberRename
{
    /// <summary>
    /// Renames <paramref name="method"/> and retargets every call and delegate node of
    /// <paramref name="classes"/> that refers to it.
    /// </summary>
    /// <param name="classes">The classes whose graphs may use the method (the whole project).</param>
    /// <param name="method">Method to rename; it must belong to a class.</param>
    /// <param name="newName">The new name.</param>
    /// <returns>A handle that reverts the rename.</returns>
    public static RenameResult RenameMethod(IEnumerable<ClassGraph> classes, MethodGraph method, string newName)
    {
        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(newName);
        TypeSpecifier declaringType = (method.Class ?? throw new ArgumentException("The method does not belong to a class.", nameof(method))).Type;
        string oldName = method.Name;
        List<BaseType> parameters = method.ArgumentTypes.ToList();
        List<Action> reverts = [];

        bool Refers(MethodSpecifier specifier) =>
            specifier.Name == oldName && specifier.DeclaringType == declaringType && specifier.ArgumentTypes.SequenceEqual(parameters);

        foreach (Node node in NodesOf(classes))
        {
            switch (node)
            {
                case CallMethodNode call when Refers(call.MethodSpecifier):
                    {
                        MethodSpecifier before = call.MethodSpecifier;
                        call.Retarget(before.WithName(newName));
                        reverts.Add(() => call.Retarget(before));
                        break;
                    }

                case MakeDelegateNode makeDelegate when Refers(makeDelegate.MethodSpecifier):
                    {
                        MethodSpecifier before = makeDelegate.MethodSpecifier;
                        makeDelegate.Retarget(before.WithName(newName));
                        reverts.Add(() => makeDelegate.Retarget(before));
                        break;
                    }
            }
        }

        method.Name = newName;
        return new RenameResult(() =>
        {
            method.Name = oldName;
            reverts.ForEach(revert => revert());
        });
    }

    /// <summary>
    /// Renames <paramref name="variable"/> and retargets every getter and setter node of
    /// <paramref name="classes"/> that refers to it.
    /// </summary>
    /// <param name="classes">The classes whose graphs may use the variable (the whole project).</param>
    /// <param name="variable">Variable to rename; it must belong to a class.</param>
    /// <param name="newName">The new name.</param>
    /// <returns>A handle that reverts the rename.</returns>
    public static RenameResult RenameVariable(IEnumerable<ClassGraph> classes, Variable variable, string newName)
    {
        ArgumentNullException.ThrowIfNull(classes);
        ArgumentNullException.ThrowIfNull(variable);
        ArgumentNullException.ThrowIfNull(newName);
        TypeSpecifier declaringType = (variable.Class ?? throw new ArgumentException("The variable does not belong to a class.", nameof(variable))).Type;
        string oldName = variable.Name;
        List<Action> reverts = [];

        foreach (Node node in NodesOf(classes))
        {
            if (node is VariableNode { Variable: { Scope: VariableScope.Member } before } access
                && before.Name == oldName && before.DeclaringType == declaringType)
            {
                access.Retarget(new VariableSpecifier(newName, before.Type, before.GetterVisibility, before.SetterVisibility, before.DeclaringType, before.Modifiers));
                reverts.Add(() => access.Retarget(before));
            }
        }

        variable.Name = newName;
        return new RenameResult(() =>
        {
            variable.Name = oldName;
            reverts.ForEach(revert => revert());
        });
    }

    private static IEnumerable<Node> NodesOf(IEnumerable<ClassGraph> classes) =>
        classes.SelectMany(GraphKeys.AllGraphs).SelectMany(graph => graph.Nodes).ToList();
}

/// <summary>A finished member rename, kept so it can be undone.</summary>
public sealed class RenameResult
{
    private readonly Action undo;

    internal RenameResult(Action undo) => this.undo = undo;

    /// <summary>Restores the member's name and every retargeted node's specifier.</summary>
    public void Undo() => undo();
}
