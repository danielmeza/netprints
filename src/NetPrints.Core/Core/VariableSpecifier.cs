#nullable enable

namespace NetPrints.Core
{
    /// <summary>
    /// Specifier describing a discovered field or property: its name, declaring and value types,
    /// getter/setter/overall visibility, and modifiers (static, indexer, etc.).
    /// </summary>
    public class VariableSpecifier
    {
        /// <summary>
        /// Name of the property without any prefixes.
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Specifier for the type this property is contained in.
        /// </summary>
        public TypeSpecifier DeclaringType
        {
            get;
            private set;
        }

        /// <summary>
        /// Specifier for the type of the property.
        /// </summary>
        public TypeSpecifier Type
        {
            get;
            set;
        }

        /// <summary>
        /// Visibility of this property's getter.
        /// </summary>
        public MemberVisibility GetterVisibility
        {
            get;
            set;
        }

        /// <summary>
        /// Visibility of this property's setter.
        /// </summary>
        public MemberVisibility SetterVisibility
        {
            get;
            set;
        }

        /// <summary>
        /// Visibility of this property.
        /// </summary>
        public MemberVisibility Visibility
        {
            get;
            set;
        } = MemberVisibility.Private;

        /// <summary>
        /// Modifiers of this variable.
        /// </summary>
        public VariableModifiers Modifiers
        {
            get;
            set;
        }

        /// <summary>
        /// Creates a variable specifier from its discovered name, type and modifiers.
        /// </summary>
        /// <param name="name">Name of the variable, without any prefixes.</param>
        /// <param name="type">Specifier for the variable's value type.</param>
        /// <param name="getterVisibility">Visibility of the getter.</param>
        /// <param name="setterVisibility">Visibility of the setter.</param>
        /// <param name="declaringType">Specifier for the type the variable is declared in.</param>
        /// <param name="modifiers">Modifiers of the variable (static, indexer, etc.).</param>
        public VariableSpecifier(string name, TypeSpecifier type, MemberVisibility getterVisibility, MemberVisibility setterVisibility,
            TypeSpecifier declaringType, VariableModifiers modifiers)
        {
            Name = name;
            Type = type;
            GetterVisibility = getterVisibility;
            SetterVisibility = setterVisibility;
            DeclaringType = declaringType;
            Modifiers = modifiers;
        }
    }
}
