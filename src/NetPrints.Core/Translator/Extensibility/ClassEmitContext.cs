#nullable enable
using System;
using System.Collections.Generic;
using NetPrints.Core;

namespace NetPrints.Translator;

/// <summary>
/// The editable emit state of one type declaration, passed to every <see cref="IClassEmitter"/>
/// (extension-points.md §3). Created by the translator.
/// </summary>
public sealed class ClassEmitContext
{
    internal ClassEmitContext(ClassGraph declaration, IEnumerable<string> baseTypes)
    {
        Class = declaration;
        BaseTypes = new List<string>(baseTypes);
    }

    /// <summary>
    /// The declaration being emitted.
    /// </summary>
    public ITypeDeclaration Declaration => Class;

    /// <summary>
    /// The class being emitted. Class-specific convenience over <see cref="Declaration"/>.
    /// </summary>
    public ClassGraph Class { get; }

    /// <summary>
    /// Namespaces to import; emitted as <c>using X;</c> before the namespace, ordinal-sorted.
    /// </summary>
    public ISet<string> Usings { get; } = new SortedSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Attribute bodies without brackets, for example <c>UClass(ClassFlags.Abstract)</c>, emitted in list order,
    /// one <c>[...]</c> per line.
    /// </summary>
    public IList<string> Attributes { get; } = new List<string>();

    /// <summary>
    /// Modifiers to add to the declaration; a subset of <c>partial</c>, <c>sealed</c>, <c>abstract</c>,
    /// <c>static</c> and <c>unsafe</c>. Anything else fails the translation with <c>NPT007</c>.
    /// </summary>
    public ISet<string> ExtraModifiers { get; } = new SortedSet<string>(StringComparer.Ordinal);

    /// <summary>
    /// Base types and interfaces in code form; prefilled from <see cref="ClassGraph.AllBaseTypes"/> and may be
    /// edited.
    /// </summary>
    public IList<string> BaseTypes { get; }
}
