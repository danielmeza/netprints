#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using NetPrints.Graph;

namespace NetPrints.Core
{
    /// <summary>
    /// Modifiers a class can have. Can be combined.
    /// </summary>
    [Flags]
    public enum ClassModifiers
    {
        /// <summary>
        /// No modifiers.
        /// </summary>
        None = 0,

        /// <summary>
        /// The class is sealed: it cannot be inherited from (C# <c>sealed</c>).
        /// </summary>
        Sealed = 8,

        /// <summary>
        /// The class has no direct instances and may declare abstract members (C# <c>abstract</c>).
        /// </summary>
        Abstract = 16,

        /// <summary>
        /// The class only has static members and cannot be instantiated (C# <c>static</c>).
        /// </summary>
        Static = 32,

        /// <summary>
        /// The class's definition is split across multiple files (C# <c>partial</c>).
        /// </summary>
        Partial = 64,

        // Deprecated
        /// <summary>
        /// Obsolete: visibility moved to <see cref="ClassGraph.Visibility"/> (<see cref="MemberVisibility"/>).
        /// Kept at value 0; not referenced anywhere in this codebase.
        /// </summary>
        [Obsolete]
        Private = 0,

        /// <summary>
        /// Obsolete: visibility moved to <see cref="ClassGraph.Visibility"/> (<see cref="MemberVisibility"/>).
        /// Kept at value 1; not referenced anywhere in this codebase.
        /// </summary>
        [Obsolete]
        Public = 1,

        /// <summary>
        /// Obsolete: visibility moved to <see cref="ClassGraph.Visibility"/> (<see cref="MemberVisibility"/>).
        /// Kept at value 2; not referenced anywhere in this codebase.
        /// </summary>
        [Obsolete]
        Protected = 2,

        /// <summary>
        /// Obsolete: visibility moved to <see cref="ClassGraph.Visibility"/> (<see cref="MemberVisibility"/>).
        /// Kept at value 4; not referenced anywhere in this codebase.
        /// </summary>
        [Obsolete]
        Internal = 4,
    }

    /// <summary>
    /// Visibility for methods, properties and other members. A <see cref="FlagsAttribute"/> enum so a set of
    /// allowed visibilities can be expressed (see <see cref="Any"/>, <see cref="ProtectedOrPublic"/>),
    /// even though a single member's own visibility is always exactly one flag.
    /// </summary>
    [Flags]
    public enum MemberVisibility
    {
        /// <summary>
        /// No visibility set; not a valid visibility for an actual member.
        /// </summary>
        Invalid = 0,

        /// <summary>
        /// Visible only within its declaring type (C# <c>private</c>).
        /// </summary>
        Private = 1,

        /// <summary>
        /// Visible from anywhere (C# <c>public</c>).
        /// </summary>
        Public = 2,

        /// <summary>
        /// Visible from its declaring type and derived types (C# <c>protected</c>).
        /// </summary>
        Protected = 4,

        /// <summary>
        /// Visible from the same assembly (C# <c>internal</c>).
        /// </summary>
        Internal = 8,

        /// <summary>
        /// Every visibility combined, for checks that accept any visibility.
        /// </summary>
        Any = Private | Public | Protected | Internal,

        /// <summary>
        /// <see cref="Protected"/> or <see cref="Public"/> combined, for checks that accept either.
        /// </summary>
        ProtectedOrPublic = Protected | Public,
    }

    /// <summary>
    /// Class graph type. Contains methods, attributes and other common things usually associated
    /// with classes.
    /// </summary>
    [DataContract]
    public partial class ClassGraph : NodeGraph
    {
        /// <summary>
        /// Return node of this class that receives the metadata for it.
        /// </summary>
        public ClassReturnNode ReturnNode
        {
            get => Nodes.OfType<ClassReturnNode>().Single();
        }

        /// <summary>
        /// Properties of this class.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<Variable> Variables { get; set; } = new ObservableRangeCollection<Variable>();

        /// <summary>
        /// Methods of this class.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<MethodGraph> Methods { get; set; } = new ObservableRangeCollection<MethodGraph>();

        /// <summary>
        /// Constructors of this class.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<ConstructorGraph> Constructors { get; set; } = new ObservableRangeCollection<ConstructorGraph>();

        /// <summary>
        /// Base / super type of this class. The ultimate base type of all classes is System.Object.
        /// </summary>
        public TypeSpecifier SuperType
        {
            get => (TypeSpecifier?)ReturnNode.SuperTypePin.InferredType?.Value ?? TypeSpecifier.FromType<object>();
        }

        /// <summary>
        /// Type this class inherits from and interfaces this class implements.
        /// </summary>
        public IEnumerable<TypeSpecifier> AllBaseTypes
        {
            get => new[] { SuperType }.Concat(ReturnNode.InterfacePins.Select(pin => (TypeSpecifier?)pin.InferredType?.Value ?? TypeSpecifier.FromType<object>()));
        }

        /// <summary>
        /// Namespace this class is in.
        /// </summary>
        [DataMember]
        public string Namespace { get; set; } = string.Empty;

        /// <summary>
        /// Name of the class without namespace.
        /// </summary>
        [DataMember]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Name of the class with namespace if any.
        /// </summary>
        public string FullName
        {
            get => string.IsNullOrWhiteSpace(Namespace) ? Name : $"{Namespace}.{Name}";
        }

        /// <summary>
        /// Modifiers this class has.
        /// </summary>
        [DataMember]
        public ClassModifiers Modifiers { get; set; }

        /// <summary>
        /// Visibility of this class.
        /// </summary>
        [DataMember]
        public MemberVisibility Visibility { get; set; } = MemberVisibility.Internal;

        /// <summary>
        /// Generic arguments this class takes.
        /// </summary>
        [DataMember]
        public ObservableRangeCollection<GenericType> DeclaredGenericArguments { get; set; } = new ObservableRangeCollection<GenericType>();

        /// <summary>
        /// TypeSpecifier describing this class.
        /// </summary>
        public TypeSpecifier Type
        {
            get => new TypeSpecifier(FullName, SuperType.IsEnum, SuperType.IsInterface,
                DeclaredGenericArguments.Cast<BaseType>().ToList());
        }

        /// <summary>
        /// Creates a class graph and its <see cref="ClassReturnNode"/>.
        /// </summary>
        public ClassGraph()
        {
            _ = new ClassReturnNode(this);
        }
    }
}
