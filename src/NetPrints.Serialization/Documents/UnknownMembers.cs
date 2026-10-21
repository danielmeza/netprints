#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace NetPrints.Serialization.Documents;

/// <summary>
/// The members of a document object that this build does not know (research R7): kept as opaque JSON values in ordinal key order and
/// written after the known members. Two collections are equal when they hold the same keys with JSON-equal values, so document records
/// that carry one still compare by value. Treat a collection that belongs to a document as read-only.
/// </summary>
public sealed class UnknownMembers : SortedDictionary<string, JsonElement>, IEquatable<UnknownMembers>
{
    /// <summary>
    /// Creates an empty collection ordered by ordinal key comparison.
    /// </summary>
    public UnknownMembers()
        : base(StringComparer.Ordinal)
    {
    }

    /// <inheritdoc/>
    public bool Equals(UnknownMembers? other) =>
        other is not null
        && Count == other.Count
        && this.All(pair => other.TryGetValue(pair.Key, out JsonElement value) && JsonElement.DeepEquals(pair.Value, value));

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as UnknownMembers);

    /// <inheritdoc/>
    public override int GetHashCode() => Count;
}
