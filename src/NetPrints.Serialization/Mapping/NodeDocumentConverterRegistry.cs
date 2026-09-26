#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization.Metadata;
using BuiltInConverters = NetPrints.Serialization.Mapping.BuiltIn;

namespace NetPrints.Serialization.Mapping;

/// <summary>
/// The set of <see cref="INodeDocumentConverter"/>s known to a document format/mapper: the built-in
/// kinds plus any extension kinds, and the <see cref="System.Text.Json.Serialization.JsonSerializerContext"/>-backed
/// resolvers extension node documents need for their own JSON shape (document-format.md §2.6,
/// extension-points.md §2).
/// </summary>
public sealed class NodeDocumentConverterRegistry
{
    /// <summary>
    /// The <c>$kind</c> names of every built-in node kind (document-format.md §1.5): a converter's
    /// <see cref="INodeDocumentConverter.Kind"/> is valid only if it contains <c>'/'</c> (an extension
    /// kind) or is one of these.
    /// </summary>
    private static readonly HashSet<string> KnownBuiltInKinds = new(StringComparer.Ordinal)
    {
        "methodEntry", "constructorEntry", "return", "classReturn", "typeReturn", "eventEntry",
        "callMethod", "constructor", "makeDelegate", "variableGetter", "variableSetter",
        "literal", "type", "makeArrayType", "makeArray", "explicitCast", "typeOf",
        "ifElse", "forLoop", "ternary", "await", "throw", "default", "reroute",
    };

    private readonly Dictionary<string, INodeDocumentConverter> byKind;
    private readonly Dictionary<Type, INodeDocumentConverter> byNodeType;
    private readonly Dictionary<Type, INodeDocumentConverter> byDocumentType;

    /// <summary>
    /// Creates a registry from an explicit list of converters and extension resolvers.
    /// </summary>
    /// <param name="converters">Every converter to register (built-in and extension).</param>
    /// <param name="extensionResolvers">One <see cref="IJsonTypeInfoResolver"/> per loaded extension
    /// that contributes node kinds, in load order.</param>
    /// <exception cref="ArgumentException">Two converters share a <see cref="INodeDocumentConverter.Kind"/>
    /// or a <see cref="INodeDocumentConverter.NodeType"/>; or a converter's <c>Kind</c> neither contains
    /// <c>'/'</c> (an extension kind) nor is one of the known built-in kind names.</exception>
    public NodeDocumentConverterRegistry(IReadOnlyList<INodeDocumentConverter> converters,
        IReadOnlyList<IJsonTypeInfoResolver> extensionResolvers)
    {
        Converters = converters;
        ExtensionResolvers = extensionResolvers;

        byKind = new Dictionary<string, INodeDocumentConverter>(StringComparer.Ordinal);
        byNodeType = new Dictionary<Type, INodeDocumentConverter>();
        byDocumentType = new Dictionary<Type, INodeDocumentConverter>();

        foreach (INodeDocumentConverter converter in converters)
        {
            bool isExtensionKind = converter.Kind.Contains('/');
            if (!isExtensionKind && !KnownBuiltInKinds.Contains(converter.Kind))
            {
                throw new ArgumentException(
                    $"Node document converter kind '{converter.Kind}' must contain '/' (an extension kind) or be a known built-in kind.",
                    nameof(converters));
            }

            if (!byKind.TryAdd(converter.Kind, converter))
            {
                throw new ArgumentException($"Duplicate node document converter kind '{converter.Kind}'.", nameof(converters));
            }

            if (!byNodeType.TryAdd(converter.NodeType, converter))
            {
                throw new ArgumentException($"Duplicate node document converter node type '{converter.NodeType}'.", nameof(converters));
            }

            if (!byDocumentType.TryAdd(converter.DocumentType, converter))
            {
                throw new ArgumentException($"Duplicate node document converter document type '{converter.DocumentType}'.", nameof(converters));
            }
        }
    }

    /// <summary>
    /// The built-in converters for the node kinds of document-format.md §1.5 implemented so far.
    /// </summary>
    public static IReadOnlyList<INodeDocumentConverter> BuiltIn { get; } = BuildBuiltIn();

    /// <summary>
    /// Every registered converter, built-in and extension.
    /// </summary>
    public IReadOnlyList<INodeDocumentConverter> Converters { get; }

    /// <summary>
    /// One <see cref="IJsonTypeInfoResolver"/> per loaded extension that contributes node kinds, in
    /// load order.
    /// </summary>
    public IReadOnlyList<IJsonTypeInfoResolver> ExtensionResolvers { get; }

    /// <summary>
    /// Returns the converter registered for <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind">The <c>$kind</c> value to look up.</param>
    /// <returns>The matching converter, or <see langword="null"/> if none is registered.</returns>
    public INodeDocumentConverter? FindByKind(string kind) => byKind.GetValueOrDefault(kind);

    /// <summary>
    /// Returns the converter registered for <paramref name="nodeType"/>.
    /// </summary>
    /// <param name="nodeType">The node's exact runtime type.</param>
    /// <returns>The matching converter, or <see langword="null"/> if none is registered.</returns>
    public INodeDocumentConverter? FindByNodeType(Type nodeType) => byNodeType.GetValueOrDefault(nodeType);

    /// <summary>
    /// Returns the converter registered for <paramref name="documentType"/>. Used when only the
    /// document side is known (a deserialized <see cref="Documents.NodeDocument"/> instance has no
    /// <c>$kind</c> string of its own to look up by; <see cref="FindByKind"/> and
    /// <see cref="FindByNodeType"/> cannot help here).
    /// </summary>
    /// <param name="documentType">The node document's exact runtime type.</param>
    /// <returns>The matching converter, or <see langword="null"/> if none is registered.</returns>
    public INodeDocumentConverter? FindByDocumentType(Type documentType) => byDocumentType.GetValueOrDefault(documentType);

    private static IReadOnlyList<INodeDocumentConverter> BuildBuiltIn()
    {
        var converters = new List<INodeDocumentConverter>();
        converters.AddRange(BuiltInConverters.EntryReturnConverters.All);
        converters.AddRange(BuiltInConverters.MemberConverters.All);
        converters.AddRange(BuiltInConverters.ValueConverters.All);
        converters.AddRange(BuiltInConverters.FlowConverters.All);
        return converters;
    }
}
