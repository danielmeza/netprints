#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace NetPrints.Core
{
    /// <summary>
    /// How a method parameter is passed.
    /// </summary>
    public enum MethodParameterPassType
    {
        /// <summary>
        /// Passed by value (no C# keyword).
        /// </summary>
        Default,

        /// <summary>
        /// Passed by reference (C# <c>ref</c>).
        /// </summary>
        Reference,

        /// <summary>
        /// Passed as an output parameter (C# <c>out</c>).
        /// </summary>
        Out,

        /// <summary>
        /// Passed by reference, read-only (C# <c>in</c>).
        /// </summary>
        In
    }

    /// <summary>
    /// Named specifier for a method parameter: its type (inherited from <see cref="Named{T}"/>), pass
    /// type, and optional explicit default value.
    /// </summary>
    [DataContract]
    public class MethodParameter : Named<BaseType>
    {
        /// <summary>
        /// How this parameter is passed.
        /// </summary>
        [DataMember]
        public MethodParameterPassType PassType
        {
            get;
            private set;
        }

        /// <summary>
        /// Whether the parameter has an explicit default value.
        /// </summary>
        [DataMember]
        public bool HasExplicitDefaultValue
        {
            get;
            private set;
        }

        /// <summary>
        /// Explicit default value for the parameter.
        /// Only valid when HasExplicitDefaultValue is true.
        /// </summary>
        [DataMember]
        public object? ExplicitDefaultValue
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates a method parameter specifier.
        /// </summary>
        /// <param name="name">Name of the parameter.</param>
        /// <param name="type">Specifier for the parameter's type.</param>
        /// <param name="passType">How the parameter is passed.</param>
        /// <param name="hasExplicitDefaultValue">Whether the parameter has an explicit default value.</param>
        /// <param name="explicitDefaultValue">Explicit default value for the parameter, valid only when <paramref name="hasExplicitDefaultValue"/> is <see langword="true"/>.</param>
        public MethodParameter(string name, BaseType type, MethodParameterPassType passType,
            bool hasExplicitDefaultValue, object? explicitDefaultValue)
            : base(name, type)
        {
            PassType = passType;
            HasExplicitDefaultValue = hasExplicitDefaultValue;
            ExplicitDefaultValue = explicitDefaultValue;
        }
    }

    /// <summary>
    /// Specifier describing a method.
    /// </summary>
    [Serializable]
    [DataContract]
    public partial class MethodSpecifier
    {
        /// <summary>
        /// Name of the method without any prefixes.
        /// </summary>
        [DataMember]
        public string Name
        {
            get;
            private set;
        }

        /// <summary>
        /// Specifier for the type this method is contained in.
        /// </summary>
        [DataMember]
        public TypeSpecifier DeclaringType
        {
            get;
            private set;
        }

        /// <summary>
        /// Named specifiers for the types this method takes as arguments.
        /// </summary>
        [DataMember]
        public IList<MethodParameter> Parameters
        {
            get;
            private set;
        }

        /// <summary>
        /// Specifiers for the types this method takes as arguments.
        /// </summary>
        public IReadOnlyList<BaseType> ArgumentTypes
        {
            get => Parameters.Select(t => (BaseType)t).ToArray();
        }

        /// <summary>
        /// Specifiers for the types this method returns.
        /// </summary>
        [DataMember]
        public IList<BaseType> ReturnTypes
        {
            get;
            private set;
        }

        /// <summary>
        /// Modifiers this method has.
        /// </summary>
        [DataMember]
        public MethodModifiers Modifiers
        {
            get;
            private set;
        }

        /// <summary>
        /// Visibility of this method.
        /// </summary>
        [DataMember]
        public MemberVisibility Visibility
        {
            get;
            private set;
        }

        /// <summary>
        /// Generic arguments this method takes.
        /// </summary>
        [DataMember]
        public IList<BaseType> GenericArguments
        {
            get;
            private set;
        }

        /// <summary>
        /// Creates a MethodSpecifier.
        /// </summary>
        /// <param name="name">Name of the method without any prefixes.</param>
        /// <param name="arguments">Specifiers for the arguments of the method.</param>
        /// <param name="returnTypes">Specifiers for the return types of the method.</param>
        /// <param name="modifiers">Modifiers of the method.</param>
        /// <param name="visibility">Visibility of the method.</param>
        /// <param name="declaringType">Specifier for the type this method is contained in.</param>
        /// <param name="genericArguments">Generic arguments this method takes.</param>
        public MethodSpecifier(string name, IEnumerable<MethodParameter> arguments,
            IEnumerable<BaseType> returnTypes, MethodModifiers modifiers, MemberVisibility visibility, TypeSpecifier declaringType,
            IList<BaseType> genericArguments)
        {
            Name = name;
            DeclaringType = declaringType;
            Parameters = arguments.ToList();
            ReturnTypes = returnTypes.ToList();
            Modifiers = modifiers;
            Visibility = visibility;
            GenericArguments = genericArguments.ToList();
        }

        /// <summary>
        /// Returns the method's declaring type (for a static method), name, parameter types,
        /// generic arguments and return types (eg. "MyClass.MyMethod(System.Int32)&lt;T&gt; : System.String").
        /// </summary>
        /// <returns>The method's display string.</returns>
        public override string ToString()
        {
            string methodString = "";

            if (Modifiers.HasFlag(MethodModifiers.Static))
            {
                methodString += $"{DeclaringType.ShortName}.";
            }

            methodString += Name;

            string argTypeString = string.Join(", ", Parameters.Select(a => a.Value.ShortName));

            methodString += $"({argTypeString})";

            if (GenericArguments.Count > 0)
            {
                string genArgTypeString = string.Join(", ", GenericArguments.Select(s => s.ShortName));
                methodString += $"<{genArgTypeString}>";
            }

            if (ReturnTypes.Count > 0)
            {
                string returnTypeString = string.Join(", ", ReturnTypes.Select(s => s.ShortName));
                methodString += $" : {returnTypeString}";
            }

            return methodString;
        }

        /// <summary>
        /// Compares this method to another <see cref="MethodSpecifier"/> by name, declaring type,
        /// argument types, return types, modifiers and generic arguments (visibility is not compared).
        /// Falls back to <see cref="object.Equals(object?)"/> for anything else.
        /// </summary>
        /// <param name="obj">Object to compare to.</param>
        /// <returns><see langword="true"/> if the two specifiers describe the same method signature.</returns>
        public override bool Equals(object? obj)
        {
            if (obj is MethodSpecifier methodSpec)
            {
                return
                    methodSpec.Name == Name
                    && methodSpec.DeclaringType == DeclaringType
                    && methodSpec.ArgumentTypes.SequenceEqual(ArgumentTypes)
                    && methodSpec.ReturnTypes.SequenceEqual(ReturnTypes)
                    && methodSpec.Modifiers == Modifiers
                    && methodSpec.GenericArguments.SequenceEqual(GenericArguments);
            }
            else
            {
                return base.Equals(obj);
            }
        }

        /// <summary>
        /// Returns a hash combining name, modifiers, generic arguments, return types, parameters,
        /// visibility and declaring type, consistent with <see cref="Equals(object?)"/>'s comparison
        /// (except that, unlike <see cref="Equals(object?)"/>, this also factors in
        /// <see cref="Visibility"/>).
        /// </summary>
        /// <returns>A hash code for this method specifier.</returns>
        public override int GetHashCode()
        {
            return HashCode.Combine(Name, Modifiers, string.Join(",", GenericArguments), string.Join(",", ReturnTypes), string.Join(",", Parameters), Visibility, DeclaringType);
        }

        /// <summary>
        /// Same as <see cref="Equals(object?)"/>, null-safe (two <see langword="null"/>s are equal).
        /// </summary>
        /// <param name="a">First method specifier, or <see langword="null"/>.</param>
        /// <param name="b">Second method specifier, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> if the two are equal or both <see langword="null"/>.</returns>
        public static bool operator ==(MethodSpecifier a, MethodSpecifier b)
        {
            if (a is null)
            {
                return b is null;
            }

            return a.Equals(b);
        }

        /// <summary>
        /// The negation of <see cref="operator ==(MethodSpecifier, MethodSpecifier)"/>.
        /// </summary>
        /// <param name="a">First method specifier, or <see langword="null"/>.</param>
        /// <param name="b">Second method specifier, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> unless the two are equal or both <see langword="null"/>.</returns>
        public static bool operator !=(MethodSpecifier a, MethodSpecifier b)
        {
            if (a is null)
            {
                return !(b is null);
            }

            return !a.Equals(b);
        }
    }
}
