#nullable enable

namespace NetPrints.Core
{
    /// <summary>The family a <see cref="PrimitiveKind"/> belongs to.</summary>
    public enum PrimitiveCategory
    {
        /// <summary><see cref="bool"/>.</summary>
        Boolean,

        /// <summary>The signed and unsigned integer types.</summary>
        Integer,

        /// <summary><see cref="float"/>, <see cref="double"/> and <see cref="decimal"/>.</summary>
        Float,

        /// <summary><see cref="char"/>.</summary>
        Character,

        /// <summary><see cref="string"/>.</summary>
        Text,
    }
}
