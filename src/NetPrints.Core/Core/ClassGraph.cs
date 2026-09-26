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

        /// <summary>
        /// This class's members that carry their own member id (data-model.md §2):
        /// <see cref="Variables"/>, then <see cref="Methods"/>, then <see cref="Constructors"/>, in
        /// that order. Each element is a <see cref="Variable"/>, <see cref="MethodGraph"/> or
        /// <see cref="ConstructorGraph"/>.
        /// </summary>
        public IEnumerable<object> Members
        {
            get => Variables.Cast<object>().Concat(Methods).Concat(Constructors);
        }

        /// <summary>
        /// Whether this class has unsaved changes: set by <see cref="MarkDirty"/> (a user edit, or a
        /// class newly created in memory), cleared by <see cref="MarkClean"/> (after a successful
        /// save or a fresh load). Not serialized; not observable in P1 (dirty-state UX is P3a).
        /// </summary>
        [IgnoreDataMember]
        public bool IsDirty { get; private set; }

        /// <summary>
        /// Marks this class dirty (it will be saved next time the project is saved).
        /// </summary>
        public void MarkDirty() => IsDirty = true;

        /// <summary>
        /// Marks this class clean (nothing to save).
        /// </summary>
        public void MarkClean() => IsDirty = false;

        private static string? GetMemberId(object member) => member switch
        {
            Variable variable => variable.Id,
            MethodGraph method => method.Id,
            ConstructorGraph constructor => constructor.Id,
            _ => throw new InvalidOperationException($"Unknown member type '{member.GetType()}'."),
        };

        private static void SetMemberId(object member, string id)
        {
            switch (member)
            {
                case Variable variable:
                    variable.Id = id;
                    break;
                case MethodGraph method:
                    method.Id = id;
                    break;
                case ConstructorGraph constructor:
                    constructor.Id = id;
                    break;
                default:
                    throw new InvalidOperationException($"Unknown member type '{member.GetType()}'.");
            }
        }

        private static string AllocateUniqueMemberId(ICollection<string> existingIds) => StableIds.AllocateUnique('m', existingIds);

        /// <summary>
        /// Gives every member of <see cref="Members"/> a unique id: a later duplicate (in member
        /// order) is assigned a fresh, unused id. A collision of two randomly-allocated ids is
        /// improbable but not impossible (data-model.md §2); callers run this before mapping a class
        /// to a document.
        /// </summary>
        /// <returns><see langword="true"/> if any member's id was changed.</returns>
        public bool EnsureUniqueMemberIds()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool changed = false;

            foreach (object member in Members)
            {
                string? id = GetMemberId(member);
                if (id is not null && seen.Add(id))
                {
                    continue;
                }

                string newId = AllocateUniqueMemberId(seen);
                SetMemberId(member, newId);
                seen.Add(newId);
                changed = true;
            }

            return changed;
        }

        /// <summary>
        /// Assigns every member of <see cref="Members"/> a member id, deterministically seeded from
        /// this class's <see cref="FullName"/> (<see cref="StableIds.SeedFor"/>), for a legacy class
        /// whose members had no ids of their own. Converting the same legacy class twice gives the
        /// same ids. Only valid immediately after
        /// <see cref="System.Runtime.Serialization.DataContractSerializer"/> deserialization, before
        /// anything reads a member's id.
        /// </summary>
        /// <exception cref="InvalidOperationException">A member of this class already has an id.</exception>
        public void AssignLegacyMemberIds()
        {
            foreach (object member in Members)
            {
                if (GetMemberId(member) is not null)
                {
                    throw new InvalidOperationException("A member of this class already has an id.");
                }
            }

            using var scope = IdGeneration.Use(new SeededIdGenerator(StableIds.SeedFor(FullName)));
            var assigned = new HashSet<string>(StringComparer.Ordinal);

            foreach (object member in Members)
            {
                string id = AllocateUniqueMemberId(assigned);
                SetMemberId(member, id);
                assigned.Add(id);
            }
        }
    }
}
