#nullable enable
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NetPrints.Core
{
    /// <summary>
    /// Constraint on generic types.
    /// </summary>
    [Serializable]
    [DataContract]
    public class GenericTypeConstraint
    {
    }

    /// <summary>
    /// An unbound generic type.
    /// </summary>
    [DataContract]
    [Serializable]
    public class GenericType : BaseType
    {
        /// <summary>
        /// Constraints for this generic type.
        /// </summary>
        public ObservableRangeCollection<GenericTypeConstraint> Constraints
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates a generic type named <paramref name="name"/> with the given constraints.
        /// </summary>
        /// <param name="name">Name of the generic type parameter.</param>
        /// <param name="constraints">Constraints for the generic type, or none.</param>
        public GenericType(string name, IEnumerable<GenericTypeConstraint>? constraints = null)
            : base(name)
        {
            if (constraints == null)
            {
                Constraints = new ObservableRangeCollection<GenericTypeConstraint>();
            }
            else
            {
                Constraints = new ObservableRangeCollection<GenericTypeConstraint>(constraints);
            }
        }

        /// <summary>
        /// Blank for generic types.
        /// </summary>
        public override string FullCodeNameUnbound
        {
            get => "";
        }

        /// <summary>
        /// Creates a GenericType from a type. Type must be a generic argument.
        /// </summary>
        /// <typeparam name="T">Type to generate GenericType for.</typeparam>
        /// <returns>GenericType for the passed type.</returns>
        public static GenericType FromType<T>()
        {
            return FromType(typeof(T));
        }

        /// <summary>
        /// Creates a GenericType from a type. Type must be a generic argument.
        /// </summary>
        /// <param name="type">Type to generate GenericType for.</param>
        /// <returns>GenericType for the passed type.</returns>
        public static GenericType FromType(Type type)
        {
            if (!type.IsGenericParameter)
            {
                throw new ArgumentException(nameof(type));
            }

            // TODO: Convert constraints
            GenericType genericType = new GenericType(type.Name);

            return genericType;
        }

        /// <summary>
        /// Compares this generic type to another <see cref="GenericType"/> by name (constraints are not
        /// checked yet, tracked by a TODO), or, unconditionally, to any <see cref="TypeSpecifier"/> --
        /// this last case is a placeholder (also tracked by a TODO) that has not been implemented to
        /// check constraint compatibility and always returns <see langword="true"/>.
        /// </summary>
        /// <param name="obj">Object to compare to.</param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="obj"/> is a <see cref="TypeSpecifier"/> (always),
        /// or a <see cref="GenericType"/> with the same <see cref="BaseType.Name"/>.
        /// </returns>
        public override bool Equals(object? obj)
        {
            if (obj is TypeSpecifier t)
            {
                // TODO: Check constraints
                return true;
            }
            else if (obj is GenericType genType)
            {
                // TODO: Check constraints
                return Name == genType.Name;
            }

            return false;
        }

        /// <summary>
        /// Returns <see cref="BaseType.Name"/>'s hash code.
        /// </summary>
        /// <returns><see cref="BaseType.Name"/>'s hash code.</returns>
        public override int GetHashCode()
        {
            return Name.GetHashCode();
        }

        /// <summary>
        /// Same as <see cref="Equals(object?)"/>.
        /// </summary>
        /// <param name="a">First generic type.</param>
        /// <param name="b">Second generic type.</param>
        /// <returns><see langword="true"/> if the two have the same name.</returns>
        public static bool operator ==(GenericType a, GenericType b)
        {
            return a.Equals(b);
        }

        /// <summary>
        /// The negation of <see cref="operator ==(GenericType, GenericType)"/>.
        /// </summary>
        /// <param name="a">First generic type.</param>
        /// <param name="b">Second generic type.</param>
        /// <returns><see langword="true"/> if the two do not have the same name.</returns>
        public static bool operator !=(GenericType a, GenericType b)
        {
            return !a.Equals(b);
        }

        /// <summary>
        /// Same as <see cref="Equals(object?)"/>: always <see langword="true"/> (see that method's
        /// remarks on this being an unimplemented placeholder).
        /// </summary>
        /// <param name="a">Generic type.</param>
        /// <param name="b">Type specifier.</param>
        /// <returns>Always <see langword="true"/>.</returns>
        public static bool operator ==(GenericType a, TypeSpecifier b)
        {
            return a.Equals(b);
        }

        /// <summary>
        /// The negation of <see cref="operator ==(GenericType, TypeSpecifier)"/>: always
        /// <see langword="false"/>.
        /// </summary>
        /// <param name="a">Generic type.</param>
        /// <param name="b">Type specifier.</param>
        /// <returns>Always <see langword="false"/>.</returns>
        public static bool operator !=(GenericType a, TypeSpecifier b)
        {
            return !a.Equals(b);
        }
    }
}
