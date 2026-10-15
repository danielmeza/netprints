#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Graph;

namespace NetPrints.Core;

/// <summary>The kinds of class member that share one name space (FR-072).</summary>
public enum MemberNameKind
{
    /// <summary>A method; it may share a name with another method (an overload).</summary>
    Method,

    /// <summary>A custom event entry.</summary>
    Event,

    /// <summary>A member variable.</summary>
    Variable,
}

/// <summary>
/// The one name-clash check of a class: its methods, event entries, member variables and its own name.
/// </summary>
public static class MemberNames
{
    /// <summary>
    /// Refuses <paramref name="name"/> when another member of <paramref name="cls"/> or the class itself uses it.
    /// A method may share a name with another method.
    /// </summary>
    /// <param name="cls">The class that owns the member.</param>
    /// <param name="name">The wanted name.</param>
    /// <param name="self">The member being named; it never clashes with itself. Null for a member that does not exist yet.</param>
    /// <param name="kind">The kind of member being named.</param>
    /// <exception cref="ArgumentException"><paramref name="name"/> is used; the message reads <c>'name' is already used by kind 'name'</c>.</exception>
    public static void ThrowIfTaken(ClassGraph cls, string name, object? self, MemberNameKind kind)
    {
        ArgumentNullException.ThrowIfNull(cls);
        ArgumentNullException.ThrowIfNull(name);

        if (kind != MemberNameKind.Method && cls.Methods.FirstOrDefault(method => !ReferenceEquals(method, self) && method.Name == name) is { } method)
        {
            throw Taken(name, "method", method.Name);
        }

        if (cls.EventGraphs.SelectMany(graph => graph.Entries).FirstOrDefault(entry => !ReferenceEquals(entry, self) && entry.EventName == name) is { } other)
        {
            throw Taken(name, "event", other.EventName);
        }

        if (cls.Variables.FirstOrDefault(variable => !ReferenceEquals(variable, self) && variable.Name == name) is { } variable)
        {
            throw Taken(name, "variable", variable.Name);
        }

        if (cls.Name == name)
        {
            throw Taken(name, "class", cls.Name);
        }
    }

    /// <summary>
    /// Returns <paramref name="baseName"/>, or <paramref name="baseName"/> with a number appended, so that no method, event entry,
    /// variable or the class itself uses it.
    /// </summary>
    /// <param name="cls">The class that will own the member.</param>
    /// <param name="baseName">The preferred name.</param>
    /// <returns>A free name.</returns>
    public static string Unique(ClassGraph cls, string baseName)
    {
        ArgumentNullException.ThrowIfNull(cls);
        ArgumentNullException.ThrowIfNull(baseName);
        List<string> taken =
        [
            .. cls.Methods.Select(method => method.Name),
            .. cls.EventGraphs.SelectMany(graph => graph.Entries).Select(entry => entry.EventName),
            .. cls.Variables.Select(variable => variable.Name),
            cls.Name,
        ];
        return NetPrintsUtil.GetUniqueName(baseName, taken);
    }

    private static ArgumentException Taken(string name, string kind, string usedBy) =>
        new($"'{name}' is already used by {kind} '{usedBy}'");
}
