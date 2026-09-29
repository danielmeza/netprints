#nullable enable
using System;
using System.Collections.Generic;
using NetPrints.Core;

namespace NetPrints.Translator;

/// <summary>
/// The editable emit state of one member, passed to every <see cref="IMemberEmitter"/>
/// (extension-points.md §3). Created by the translator.
/// </summary>
public sealed class MemberEmitContext
{
    internal MemberEmitContext(ClassGraph declaration, EmittedMemberKind kind, string name, object model)
    {
        Class = declaration;
        Kind = kind;
        Name = name;
        Model = model;
    }

    /// <summary>
    /// The declaration the member belongs to.
    /// </summary>
    public ITypeDeclaration Declaration => Class;

    /// <summary>
    /// The class the member belongs to. Class-specific convenience over <see cref="Declaration"/>.
    /// </summary>
    public ClassGraph Class { get; }

    /// <summary>
    /// What kind of member this is.
    /// </summary>
    public EmittedMemberKind Kind { get; }

    /// <summary>
    /// Member name; the class name for a constructor.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The model behind the member: a <see cref="Variable"/>, <see cref="MethodGraph"/> or
    /// <see cref="ConstructorGraph"/> (an event entry node once events exist).
    /// </summary>
    public object Model { get; }

    /// <summary>
    /// Attribute bodies without brackets, emitted in list order, one <c>[...]</c> per line.
    /// </summary>
    public IList<string> Attributes { get; } = new List<string>();

    /// <summary>
    /// Modifiers to add to the member; a subset of <c>abstract</c>, <c>new</c>, <c>override</c>, <c>partial</c>,
    /// <c>readonly</c>, <c>sealed</c>, <c>static</c>, <c>unsafe</c> and <c>virtual</c>. Anything else fails the
    /// translation with <c>NPT007</c>. Modifiers the member already has are not repeated.
    /// </summary>
    public ISet<string> ExtraModifiers { get; } = new SortedSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Properties only: emit <c>partial T X { get; set; }</c> without bodies or a backing field. Setting it on
    /// any other member fails the translation with <c>NPT007</c>.
    /// </summary>
    public bool DeclarePartial { get; set; }
}
