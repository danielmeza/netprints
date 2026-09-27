#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using NetPrints.Core;

namespace NetPrints.Reflection;

/// <summary>
/// Structural equality for the specifier types that define none of their own, used to drop duplicates
/// across providers.
/// </summary>
internal static class SpecifierComparers
{
    /// <summary>
    /// Two variables are the same when declaring type, name, type and modifiers match.
    /// </summary>
    public static IEqualityComparer<VariableSpecifier> Variables { get; } = new VariableComparer();

    /// <summary>
    /// Two constructors are the same when their <see cref="ConstructorSpecifier.ToString"/> forms match
    /// (declaring type and argument list) and their declaring types are equal.
    /// </summary>
    public static IEqualityComparer<ConstructorSpecifier> Constructors { get; } = new ConstructorComparer();

    private sealed class VariableComparer : IEqualityComparer<VariableSpecifier>
    {
        public bool Equals(VariableSpecifier? x, VariableSpecifier? y)
        {
            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            return x.Name == y.Name && x.DeclaringType == y.DeclaringType && x.Type == y.Type && x.Modifiers == y.Modifiers;
        }

        public int GetHashCode(VariableSpecifier obj) => HashCode.Combine(obj.Name, obj.DeclaringType, obj.Modifiers);
    }

    private sealed class ConstructorComparer : IEqualityComparer<ConstructorSpecifier>
    {
        public bool Equals(ConstructorSpecifier? x, ConstructorSpecifier? y)
        {
            if (x is null || y is null)
            {
                return x is null && y is null;
            }

            return x.DeclaringType == y.DeclaringType && x.ToString() == y.ToString();
        }

        public int GetHashCode(ConstructorSpecifier obj) => HashCode.Combine(obj.DeclaringType, obj.ToString());
    }
}
