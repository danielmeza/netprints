#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;

namespace NetPrints.Graph
{
    /// <summary>
    /// The identity of a class member: its kind, declaring type, name and, for methods and events, parameter types.
    /// Two members with the same name in different classes, or overloads, are different keys.
    /// </summary>
    /// <param name="Kind">The member's kind.</param>
    /// <param name="DeclaringType">The type that declares the member.</param>
    /// <param name="Name">The member's name.</param>
    /// <param name="Parameters">The parameter types, in order; empty for a variable.</param>
    public readonly record struct MemberKey(MemberKind Kind, TypeSpecifier DeclaringType, string Name, IReadOnlyList<BaseType> Parameters)
    {
        /// <summary>
        /// Whether <paramref name="other"/> names the same member: equal kind, declaring type and name, and the
        /// same parameter types in the same order (the lists themselves need not be the same instance).
        /// </summary>
        /// <param name="other">The key to compare with.</param>
        /// <returns><see langword="true"/> if the keys are equal.</returns>
        public bool Equals(MemberKey other) =>
            Kind == other.Kind
            && Equals(DeclaringType, other.DeclaringType)
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && (Parameters ?? []).SequenceEqual(other.Parameters ?? []);

        /// <summary>
        /// A hash code over the kind, declaring type, name and the parameter types in order.
        /// </summary>
        /// <returns>The hash code, consistent with <see cref="Equals(MemberKey)"/>.</returns>
        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(Kind);
            hash.Add(DeclaringType);
            hash.Add(Name, StringComparer.Ordinal);
            foreach (BaseType parameter in Parameters ?? [])
            {
                hash.Add(parameter);
            }

            return hash.ToHashCode();
        }
    }
}
