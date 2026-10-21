#nullable enable
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using NetPrints.Serialization.Documents;

namespace NetPrints.Serialization.Mapping;

/// <summary>
/// Carries the members a document record holds that this build does not know (FR-007, research R7) across the model: the mapper
/// remembers them against the model object a record became on load and writes them back from it on save, so the editor's open and
/// save keeps them. A model object a copy or an edit replaces starts without any; the model itself stays unaware of them.
/// </summary>
internal static class PreservedMembers
{
    /// <summary>The slot for the record that describes the object itself (a class, member, node, pin or local variable).</summary>
    internal const string Member = "member";

    /// <summary>The slot for the graph record of an object that is a graph.</summary>
    internal const string Graph = "graph";

    /// <summary>The slot for the accessor record of a variable's getter or setter graph.</summary>
    internal const string Accessor = "accessor";

    private const string ConnectionPrefix = "connection:";

    private static readonly ConditionalWeakTable<object, Dictionary<string, UnknownMembers>> Table = new();

    /// <summary>Remembers <paramref name="members"/> against <paramref name="owner"/> and <paramref name="slot"/>; nothing is kept for null or empty.</summary>
    internal static void Remember(object owner, string slot, UnknownMembers? members)
    {
        if (members is not { Count: > 0 })
        {
            return;
        }

        Table.GetOrCreateValue(owner)[slot] = members;
    }

    /// <summary>Gets what <see cref="Remember"/> kept for <paramref name="owner"/> and <paramref name="slot"/>, or <see langword="null"/>.</summary>
    internal static UnknownMembers? Recall(object owner, string slot) =>
        Table.TryGetValue(owner, out Dictionary<string, UnknownMembers>? slots) && slots.TryGetValue(slot, out UnknownMembers? members) ? members : null;

    /// <summary>Remembers the members of a connection record against the graph that holds it.</summary>
    internal static void RememberConnection(object graph, ConnectionDocument connection) =>
        Remember(graph, ConnectionPrefix + connection.From + ">" + connection.To, connection.ExtensionData);

    /// <summary>Gets the members remembered for the connection from <paramref name="from"/> to <paramref name="to"/> in <paramref name="graph"/>.</summary>
    internal static UnknownMembers? RecallConnection(object graph, string from, string to) =>
        Recall(graph, ConnectionPrefix + from + ">" + to);
}
