#nullable enable
using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace NetPrints.Core
{
    /// <summary>
    /// The table of built-in primitive types: one <see cref="PrimitiveKind"/> row each. Defaults, parsing, formatting,
    /// literal suffixes, C# keywords and pin families come from here instead of from lists kept in each project.
    /// </summary>
    public static class PrimitiveKinds
    {
        private static readonly PrimitiveKind[] Rows =
        [
            PrimitiveKind.Boolean(),
            PrimitiveKind.Number<byte>("byte", PrimitiveCategory.Integer),
            PrimitiveKind.Number<sbyte>("sbyte", PrimitiveCategory.Integer),
            PrimitiveKind.Number<short>("short", PrimitiveCategory.Integer),
            PrimitiveKind.Number<ushort>("ushort", PrimitiveCategory.Integer),
            PrimitiveKind.Number<int>("int", PrimitiveCategory.Integer),
            PrimitiveKind.Number<uint>("uint", PrimitiveCategory.Integer, "U"),
            PrimitiveKind.Number<long>("long", PrimitiveCategory.Integer, "L"),
            PrimitiveKind.Number<ulong>("ulong", PrimitiveCategory.Integer, "UL"),
            PrimitiveKind.Number<float>("float", PrimitiveCategory.Float, "F", "R"),
            PrimitiveKind.Number<double>("double", PrimitiveCategory.Float, "D", "R"),
            PrimitiveKind.Number<decimal>("decimal", PrimitiveCategory.Float, "M"),
            PrimitiveKind.Character(),
            PrimitiveKind.Text(),
        ];

        private static readonly FrozenDictionary<string, PrimitiveKind> ByFullName = BuildIndex();

        /// <summary>Gets every primitive, in a fixed order: <see cref="bool"/>, the integers, the floating-point types, <see cref="char"/> and <see cref="string"/>.</summary>
        public static IReadOnlyList<PrimitiveKind> All { get; } = Array.AsReadOnly(Rows);

        /// <summary>Finds the row of a type.</summary>
        /// <param name="fullName">The full name of the type, for example <c>System.Int32</c>.</param>
        /// <param name="kind">The row, when the type is a primitive.</param>
        /// <returns><see langword="true"/> when <paramref name="fullName"/> names a primitive.</returns>
        public static bool TryGet([NotNullWhen(true)] string? fullName, [NotNullWhen(true)] out PrimitiveKind? kind)
        {
            if (fullName is null)
            {
                kind = null;
                return false;
            }

            return ByFullName.TryGetValue(fullName, out kind);
        }

        private static FrozenDictionary<string, PrimitiveKind> BuildIndex()
        {
            var index = new Dictionary<string, PrimitiveKind>(StringComparer.Ordinal);
            foreach (PrimitiveKind row in Rows)
            {
                index.Add(row.FullName, row);
            }

            return index.ToFrozenDictionary(StringComparer.Ordinal);
        }
    }
}
