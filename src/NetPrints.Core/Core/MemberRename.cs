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
        var key = new MemberKey(MemberKind.Method, declaringType, oldName, method.ArgumentTypes.ToList());
        return Rename(classes, key, newName, () => method.Name = newName, () => method.Name = oldName);
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
        var key = new MemberKey(MemberKind.Variable, declaringType, oldName, []);
        return Rename(classes, key, newName, () => variable.Name = newName, () => variable.Name = oldName);
    }

    private static RenameResult Rename(IEnumerable<ClassGraph> classes, MemberKey key, string newName, Action applyOwnName, Action revertOwnName)
    {
        List<Action> reverts = [];

        foreach (IMemberReferencingNode node in NodesOf(classes).OfType<IMemberReferencingNode>().Where(node => node.RefersTo(key)))
        {
            reverts.Add(node.Retarget(key, newName));
        }

        applyOwnName();
        return new RenameResult(() =>
        {
            revertOwnName();
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
