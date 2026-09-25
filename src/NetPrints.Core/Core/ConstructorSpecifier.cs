#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace NetPrints.Core
{
    /// <summary>
    /// Specifier describing a constructor.
    /// </summary>
    [Serializable]
    [DataContract]
    public partial class ConstructorSpecifier
    {
        /// <summary>
        /// Specifier for the type this constructor is for.
        /// </summary>
        [DataMember]
        public TypeSpecifier DeclaringType
        {
            get;
            private set;
        }

        /// <summary>
        /// Specifiers for the arguments this constructor takes.
        /// </summary>
        [DataMember]
        public IList<MethodParameter> Arguments
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates a ConstructorSpecifier given specifiers for the constructor's arguments and the type it is for.
        /// </summary>
        /// <param name="arguments">Specifiers for the arguments the constructor takes.</param>
        /// <param name="declaringType">Specifier for the type the constructor is for.</param>
        public ConstructorSpecifier(IEnumerable<MethodParameter> arguments, TypeSpecifier declaringType)
        {
            DeclaringType = declaringType;
            Arguments = arguments.ToList();
        }

        /// <summary>
        /// Returns the declaring type's name followed by the argument list in parentheses (eg.
        /// "MyClass(System.Int32, System.String)").
        /// </summary>
        /// <returns>The declaring type's name followed by the argument list in parentheses.</returns>
        public override string ToString()
        {
            string constructorString = "";

            string argTypeString = string.Join(", ", Arguments);

            constructorString += $"{DeclaringType.Name}({argTypeString})";

            return constructorString;
        }
    }
}
