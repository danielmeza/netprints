#nullable enable
using System.Runtime.Serialization;

namespace NetPrints.Core
{
    /// <summary>
    /// Contains a value and its name. Can be implicitly
    /// converted to the class itself.
    /// </summary>
    /// <typeparam name="T">Type of the value.</typeparam>
    [DataContract]
    [KnownType(typeof(MethodParameter))]
    public class Named<T>
    {
        /// <summary>
        /// The name.
        /// </summary>
        [DataMember]
        public string Name { get; set; }

        /// <summary>
        /// The named value.
        /// </summary>
        [DataMember]
        public T Value { get; set; }

        /// <summary>
        /// Creates a name/value pair.
        /// </summary>
        /// <param name="name">The name.</param>
        /// <param name="type">The named value.</param>
        public Named(string name, T type)
        {
            Name = name;
            Value = type;
        }

        /// <summary>
        /// Unwraps <paramref name="namedValue"/>'s <see cref="Value"/>.
        /// </summary>
        /// <param name="namedValue">Named value to unwrap.</param>
        /// <returns><paramref name="namedValue"/>'s <see cref="Value"/>.</returns>
        public static implicit operator T(Named<T> namedValue) => namedValue.Value;

        /// <summary>
        /// Returns <see cref="Name"/> followed by ": " and <see cref="Value"/>.
        /// </summary>
        /// <returns><see cref="Name"/> followed by ": " and <see cref="Value"/>.</returns>
        public override string ToString()
        {
            return $"{Name}: {Value}";
        }
    }
}
