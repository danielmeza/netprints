#nullable enable
using System.Collections.Generic;
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
    public readonly record struct MemberKey(MemberKind Kind, TypeSpecifier DeclaringType, string Name, IReadOnlyList<BaseType> Parameters);
}
