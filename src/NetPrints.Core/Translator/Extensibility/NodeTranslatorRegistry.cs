#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace NetPrints.Translator;

/// <summary>
/// Maps exact node runtime types to their <see cref="INodeTranslator"/> (extension-points.md §2.1).
/// Immutable: <see cref="With"/> returns a new registry.
/// </summary>
public sealed class NodeTranslatorRegistry
{
    private readonly IReadOnlyDictionary<Type, INodeTranslator> translators;

    /// <summary>
    /// Creates a registry over a copy of <paramref name="translators"/>.
    /// </summary>
    /// <param name="translators">Translators keyed by the exact runtime type of the nodes they handle.</param>
    public NodeTranslatorRegistry(IReadOnlyDictionary<Type, INodeTranslator> translators)
    {
        ArgumentNullException.ThrowIfNull(translators);
        this.translators = new Dictionary<Type, INodeTranslator>(translators);
    }

    /// <summary>
    /// The translators of every built-in node kind.
    /// </summary>
    public static NodeTranslatorRegistry BuiltIn { get; } = new(BuiltInNodeTranslators.Create());

    /// <summary>
    /// Finds the translator registered for exactly <paramref name="nodeType"/>.
    /// </summary>
    /// <param name="nodeType">Runtime type of a node.</param>
    /// <returns>The translator, or <see langword="null"/> when none is registered.</returns>
    public INodeTranslator? Find(Type nodeType)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        return translators.TryGetValue(nodeType, out INodeTranslator? translator) ? translator : null;
    }

    /// <summary>
    /// Returns a registry with <paramref name="translator"/> added for <paramref name="nodeType"/>.
    /// </summary>
    /// <param name="nodeType">Exact runtime type of the nodes the translator handles.</param>
    /// <param name="translator">Translator to add.</param>
    /// <returns>A new registry; this one is unchanged.</returns>
    /// <exception cref="ArgumentException">A translator is already registered for <paramref name="nodeType"/>.</exception>
    public NodeTranslatorRegistry With(Type nodeType, INodeTranslator translator)
    {
        ArgumentNullException.ThrowIfNull(nodeType);
        ArgumentNullException.ThrowIfNull(translator);

        if (translators.ContainsKey(nodeType))
        {
            throw new ArgumentException($"A translator is already registered for '{nodeType}'.", nameof(nodeType));
        }

        return new NodeTranslatorRegistry(translators.Append(new KeyValuePair<Type, INodeTranslator>(nodeType, translator))
            .ToDictionary(pair => pair.Key, pair => pair.Value));
    }
}
