#nullable enable
using System;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using NetPrints.Graph;

namespace NetPrints.Core
{
    /// <summary>
    /// Modifiers variables can have. Can be combined.
    /// </summary>
    [Flags]
    public enum VariableModifiers
    {
        /// <summary>
        /// No modifiers.
        /// </summary>
        None = 0,

        /// <summary>
        /// The variable is read-only (a C# <c>readonly</c> field or a get-only property).
        /// </summary>
        ReadOnly = 8,

        /// <summary>
        /// The variable is a compile-time constant (C# <c>const</c>).
        /// </summary>
        Const = 16,

        /// <summary>
        /// The variable is static rather than an instance member.
        /// </summary>
        Static = 32,

        /// <summary>
        /// The variable hides an inherited member of the same name (C# <c>new</c>).
        /// </summary>
        New = 64,

        /// <summary>
        /// Obsolete: visibility moved to <see cref="Variable.Visibility"/> (<see cref="MemberVisibility"/>).
        /// Kept at value 0; not referenced anywhere in this codebase.
        /// </summary>
        [Obsolete]
        Private = 0,

        /// <summary>
        /// Obsolete: visibility moved to <see cref="Variable.Visibility"/> (<see cref="MemberVisibility"/>).
        /// Kept at value 1; not referenced anywhere in this codebase.
        /// </summary>
        [Obsolete]
        Public = 1,

        /// <summary>
        /// Obsolete: visibility moved to <see cref="Variable.Visibility"/> (<see cref="MemberVisibility"/>).
        /// Kept at value 2; not referenced anywhere in this codebase.
        /// </summary>
        [Obsolete]
        Protected = 2,

        /// <summary>
        /// Obsolete: visibility moved to <see cref="Variable.Visibility"/> (<see cref="MemberVisibility"/>).
        /// Kept at value 4; not referenced anywhere in this codebase.
        /// </summary>
        [Obsolete]
        Internal = 4,
    }

    /// <summary>
    /// Specifier describing a property of a class.
    /// </summary>
    [Serializable]
    public partial class Variable : ModelObject
    {
        /// <summary>
        /// Name of the variable without any prefixes.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Specifier))]
        public partial string Name { get; set; }

        /// <summary>
        /// Class this variable is contained in.
        /// </summary>
        public ClassGraph Class
        {
            get;
            private set;
        }

        /// <summary>
        /// Specifier for the type of the variable.
        /// </summary>
        public TypeSpecifier Type => TypeGraph.ReturnType;

        /// <summary>
        /// Get method for this variable. Can be null.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPublicGetter))]
        [NotifyPropertyChangedFor(nameof(HasAccessors))]
        [NotifyPropertyChangedFor(nameof(HasPublicSetter))]
        [NotifyPropertyChangedFor(nameof(Specifier))]
        public partial MethodGraph? GetterMethod { get; set; }

        /// <summary>
        /// Set method for this variable. Can be null.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPublicSetter))]
        [NotifyPropertyChangedFor(nameof(HasAccessors))]
        [NotifyPropertyChangedFor(nameof(HasPublicGetter))]
        [NotifyPropertyChangedFor(nameof(Specifier))]
        public partial MethodGraph? SetterMethod { get; set; }

        /// <summary>
        /// Graph specifying the type of this variable.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Type))]
        [NotifyPropertyChangedFor(nameof(Specifier))]
        public partial TypeGraph TypeGraph { get; set; }

        /// <summary>
        /// Whether this variable has a public getter.
        /// </summary>
        public bool HasPublicGetter
        {
            get => HasAccessors ?
                (GetterMethod?.Visibility == MemberVisibility.Public) :
                Visibility == MemberVisibility.Public;
        }

        /// <summary>
        /// Whether this variable has a public setter.
        /// </summary>
        public bool HasPublicSetter
        {
            get => HasAccessors ?
                (SetterMethod?.Visibility == MemberVisibility.Public) :
                Visibility == MemberVisibility.Public;
        }

        /// <summary>
        /// Whether this variable declares a get or set method.
        /// </summary>
        public bool HasAccessors
        {
            get => GetterMethod != null || SetterMethod != null;
        }

        /// <summary>
        /// Whether this property is static.
        /// </summary>
        [Obsolete]
        public bool IsStatic
        {
            get;
            private set;
        }

        /// <summary>
        /// Visibility of this property.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPublicGetter))]
        [NotifyPropertyChangedFor(nameof(HasPublicSetter))]
        [NotifyPropertyChangedFor(nameof(Specifier))]
        public partial MemberVisibility Visibility { get; set; } = MemberVisibility.Private;

        /// <summary>
        /// Modifiers of this variable.
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(Specifier))]
        public partial VariableModifiers Modifiers { get; set; }

        /// <summary>
        /// A fresh <see cref="VariableSpecifier"/> snapshotting this variable's current name, type,
        /// getter/setter visibility (the accessor method's own visibility if it has one, otherwise
        /// <see cref="Visibility"/>) and modifiers. Recomputed on every access; not cached.
        /// </summary>
        public VariableSpecifier Specifier
        {
            get => new VariableSpecifier(Name, Type, GetterMethod?.Visibility ?? Visibility, SetterMethod?.Visibility ?? Visibility, Class.Type, Modifiers);
        }

        /// <summary>
        /// This variable's member id (data-model.md §2), used as its type graph's and accessors' graph
        /// keys (<c>&lt;Id&gt;/type</c>, <c>/get</c>, <c>/set</c>). Assigned once, in the constructor,
        /// from <see cref="IdGeneration.Current"/>; the mapper overwrites it from the document.
        /// </summary>
        public string Id { get; internal set; }

        /// <summary>
        /// Creates a PropertySpecifier.
        /// </summary>
        /// <param name="cls">Graph the variable is a part of.</param>
        /// <param name="name">Name of the property.</param>
        /// <param name="type">Specifier for the type of this property.</param>
        /// <param name="getter">Get method for the property. Can be null if there is none.</param>
        /// <param name="setter">Set method for the property. Can be null if there is none.</param>
        /// <param name="modifiers">Modifiers of the variable.</param>
        public Variable(ClassGraph cls, string name, TypeSpecifier type, MethodGraph? getter,
            MethodGraph? setter, VariableModifiers modifiers)
        {
            Id = IdGeneration.Current.NewId('m');
            Class = cls;
            Name = name;
            GetterMethod = getter;
            SetterMethod = setter;
            Modifiers = modifiers;

            // Create a type graph with the type as its return type. OwningClass lets GraphKeys.For key it
            // as "<variable id>/type" (document-format.md §1.4.1).
            const int typeNodePositionX = 500;
            const int typeNodePositionY = 300;
            const int returnNodePositionX = 800;
            TypeGraph = new TypeGraph { OwningClass = cls };
            NodeOutputTypePin typePin = GraphUtil.CreateNestedTypeNode(TypeGraph, type, typeNodePositionX, typeNodePositionY).OutputTypePins[0];
            TypeGraph.ReturnNode.PositionX = returnNodePositionX;
            TypeGraph.ReturnNode.PositionY = typeNodePositionY;
            GraphUtil.ConnectTypePins(typePin, TypeGraph.ReturnNode.TypePin);
        }
    }
}
