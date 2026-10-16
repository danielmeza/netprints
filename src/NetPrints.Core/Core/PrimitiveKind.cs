#nullable enable
using System;
using System.Globalization;
using System.Numerics;

namespace NetPrints.Core
{
    /// <summary>
    /// One row of <see cref="PrimitiveKinds"/>: what the editor, the serializer and the translator need to know about
    /// a built-in value type, kept in one place. Every conversion uses the invariant culture.
    /// </summary>
    public abstract class PrimitiveKind
    {
        private PrimitiveKind(Type clrType, string keyword, PrimitiveCategory category, object defaultValue, string literalSuffix)
        {
            ClrType = clrType;
            Keyword = keyword;
            Category = category;
            DefaultValue = defaultValue;
            LiteralSuffix = literalSuffix;
        }

        /// <summary>Gets the runtime type of the primitive.</summary>
        public Type ClrType { get; }

        /// <summary>Gets the full name of the type, for example <c>System.Int32</c>; the key the graph file stores.</summary>
        public string FullName => ClrType.FullName ?? ClrType.Name;

        /// <summary>Gets the C# keyword, for example <c>int</c>.</summary>
        public string Keyword { get; }

        /// <summary>Gets the family of the type.</summary>
        public PrimitiveCategory Category { get; }

        /// <summary>Gets the value an unconnected input of this type starts with: zero, <see langword="false"/>, the null character or the empty string.</summary>
        public object DefaultValue { get; }

        /// <summary>Gets the suffix a C# literal of this type carries, such as <c>L</c> for <see cref="long"/>; empty when there is none.</summary>
        public string LiteralSuffix { get; }

        /// <summary>Gets a value indicating whether the type is a number the numeric editor edits.</summary>
        public bool IsNumeric => Category is PrimitiveCategory.Integer or PrimitiveCategory.Float;

        /// <summary>Parses the stored text of a value.</summary>
        /// <param name="text">The text, in the invariant culture.</param>
        /// <returns>The value, boxed as <see cref="ClrType"/>.</returns>
        /// <exception cref="FormatException"><paramref name="text"/> is not a valid literal of this type.</exception>
        /// <exception cref="OverflowException">The number is outside the range of this type.</exception>
        public abstract object Parse(string text);

        /// <summary>Formats a value as the text a graph file stores.</summary>
        /// <param name="value">A boxed <see cref="ClrType"/>.</param>
        /// <returns>The text, in the invariant culture.</returns>
        /// <exception cref="ArgumentException"><paramref name="value"/> is not of this type.</exception>
        public abstract string Format(object value);

        /// <summary>Converts a value of another primitive type to this one.</summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>The value, boxed as <see cref="ClrType"/>.</returns>
        /// <exception cref="InvalidCastException">The conversion is not supported.</exception>
        /// <exception cref="FormatException">A text value is not a valid literal.</exception>
        /// <exception cref="OverflowException">The value is outside the range of this type.</exception>
        public object Convert(object value)
        {
            ArgumentNullException.ThrowIfNull(value);
            return System.Convert.ChangeType(value, ClrType, CultureInfo.InvariantCulture);
        }

        internal static PrimitiveKind Number<T>(string keyword, PrimitiveCategory category, string literalSuffix = "", string? format = null)
            where T : INumberBase<T>
        {
            return new NumberKind<T>(keyword, category, literalSuffix, format);
        }

        internal static PrimitiveKind Boolean() => new BooleanKind();

        internal static PrimitiveKind Character() => new CharacterKind();

        internal static PrimitiveKind Text() => new TextKind();

        private sealed class NumberKind<T> : PrimitiveKind
            where T : INumberBase<T>
        {
            private readonly string? format;

            internal NumberKind(string keyword, PrimitiveCategory category, string literalSuffix, string? format)
                : base(typeof(T), keyword, category, T.Zero, literalSuffix)
            {
                this.format = format;
            }

            public override object Parse(string text) => T.Parse(text, CultureInfo.InvariantCulture);

            public override string Format(object value) =>
                value is T typed
                    ? typed.ToString(format, CultureInfo.InvariantCulture)
                    : throw new ArgumentException($"'{value}' is not a {FullName}.", nameof(value));
        }

        private sealed class BooleanKind : PrimitiveKind
        {
            internal BooleanKind()
                : base(typeof(bool), "bool", PrimitiveCategory.Boolean, false, string.Empty)
            {
            }

            public override object Parse(string text) => bool.Parse(text);

            public override string Format(object value) =>
                value is bool typed
                    ? (typed ? "true" : "false")
                    : throw new ArgumentException($"'{value}' is not a {FullName}.", nameof(value));
        }

        private sealed class CharacterKind : PrimitiveKind
        {
            internal CharacterKind()
                : base(typeof(char), "char", PrimitiveCategory.Character, '\0', string.Empty)
            {
            }

            public override object Parse(string text) =>
                text.Length == 1 ? text[0] : throw new FormatException($"'{text}' is not a single character.");

            public override string Format(object value) =>
                value is char typed
                    ? typed.ToString()
                    : throw new ArgumentException($"'{value}' is not a {FullName}.", nameof(value));
        }

        private sealed class TextKind : PrimitiveKind
        {
            internal TextKind()
                : base(typeof(string), "string", PrimitiveCategory.Text, string.Empty, string.Empty)
            {
            }

            public override object Parse(string text) => text;

            public override string Format(object value) =>
                value is string typed
                    ? typed
                    : throw new ArgumentException($"'{value}' is not a {FullName}.", nameof(value));
        }
    }
}
