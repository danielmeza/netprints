#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
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
    public partial class ClassGraph : NodeGraph, ITypeDeclaration
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
        public ObservableRangeCollection<Variable> Variables { get; set; } = new ObservableRangeCollection<Variable>();

        /// <summary>
        /// Methods of this class.
        /// </summary>
        public ObservableRangeCollection<MethodGraph> Methods { get; set; } = new ObservableRangeCollection<MethodGraph>();

        /// <summary>
        /// Constructors of this class.
        /// </summary>
        public ObservableRangeCollection<ConstructorGraph> Constructors { get; set; } = new ObservableRangeCollection<ConstructorGraph>();

        /// <summary>
        /// Event graphs of this class (US4).
        /// </summary>
        public ObservableRangeCollection<EventGraph> EventGraphs { get; set; } = new ObservableRangeCollection<EventGraph>();

        /// <summary>
        /// Base / super type of this class. The ultimate base type of all classes is System.Object.
        /// </summary>
        public TypeSpecifier SuperType
        {
            get => (TypeSpecifier?)ReturnNode.SuperTypePin.InferredType?.Value ?? TypeSpecifier.FromType<object>();
        }

        /// <summary>
        /// Type this class inherits from and interfaces this class implements. An interface pin with no
        /// inferred type (added but never connected) contributes no interface here rather than
        /// defaulting to <c>System.Object</c>, which would duplicate <see cref="SuperType"/>.
        /// </summary>
        public IEnumerable<TypeSpecifier> AllBaseTypes
        {
            get => new[] { SuperType }.Concat(ReturnNode.InterfacePins
                .Select(pin => (TypeSpecifier?)pin.InferredType?.Value)
                .OfType<TypeSpecifier>());
        }

        /// <summary>
        /// Namespace this class is in.
        /// </summary>
        public string Namespace { get; set; } = string.Empty;

        /// <summary>
        /// Name of the class without namespace.
        /// </summary>
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
        public ClassModifiers Modifiers { get; set; }

        /// <summary>
        /// Visibility of this class.
        /// </summary>
        public MemberVisibility Visibility { get; set; } = MemberVisibility.Internal;

        /// <summary>
        /// Generic arguments this class takes.
        /// </summary>
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
        /// <see cref="Variables"/>, then <see cref="Methods"/>, then <see cref="Constructors"/>, then
        /// <see cref="EventGraphs"/>, in that order. Each element is a <see cref="Variable"/>,
        /// <see cref="MethodGraph"/>, <see cref="ConstructorGraph"/> or <see cref="EventGraph"/>.
        /// </summary>
        public IEnumerable<object> Members
        {
            get => Variables.Cast<object>().Concat(Methods).Concat(Constructors).Concat(EventGraphs);
        }

        /// <summary>
        /// Whether this class has unsaved changes: set by <see cref="MarkDirty"/> (a user edit, or a
        /// class newly created in memory), cleared by <see cref="MarkClean"/> (after a successful
        /// save or a fresh load). Not serialized; not observable in P1 (dirty-state UX is P3a).
        /// </summary>
        public bool IsDirty { get; private set; }

        /// <summary>
        /// Marks this class dirty (it will be saved next time the project is saved).
        /// </summary>
        public void MarkDirty() => IsDirty = true;

        /// <summary>
        /// Marks this class clean (nothing to save).
        /// </summary>
        public void MarkClean() => IsDirty = false;

        /// <summary>
        /// Full path of the graph file this class was loaded from, or <see langword="null"/> for a
        /// class created in memory and never yet saved. Set by <c>ProjectPersistence.LoadAsync</c>
        /// (project-system.md, T056); read by <see cref="Project.GetGraphFilePath"/>. Not serialized.
        /// </summary>
        public string? LoadedGraphFilePath { get; internal set; }

        private static string? GetMemberId(object member) => member switch
        {
            Variable variable => variable.Id,
            MethodGraph method => method.Id,
            ConstructorGraph constructor => constructor.Id,
            EventGraph eventGraph => eventGraph.Id,
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
                case EventGraph eventGraph:
                    eventGraph.Id = id;
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
    }
}
