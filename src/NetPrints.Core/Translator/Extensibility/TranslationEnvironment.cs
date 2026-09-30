#nullable enable
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Translator;

/// <summary>
/// Everything a <see cref="ClassTranslator"/> takes from extensions (extension-points.md §2.1, §3): node
/// translators and the class and member emitters, each list in registry order.
/// </summary>
/// <param name="Nodes">Node translators by node type.</param>
/// <param name="ClassEmitters">Emitters that run once per translated type declaration.</param>
/// <param name="MemberEmitters">Emitters that run once per translated member.</param>
[method: Experimental(ExperimentalApiIds.Emitters, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record TranslationEnvironment(
    NodeTranslatorRegistry Nodes,
    [property: Experimental(ExperimentalApiIds.Emitters, UrlFormat = ExperimentalApiIds.UrlFormat)] IReadOnlyList<IClassEmitter> ClassEmitters,
    [property: Experimental(ExperimentalApiIds.Emitters, UrlFormat = ExperimentalApiIds.UrlFormat)] IReadOnlyList<IMemberEmitter> MemberEmitters)
{
    /// <summary>
    /// Splits the environment into its parts.
    /// </summary>
    /// <param name="Nodes">Node translators by node type.</param>
    /// <param name="ClassEmitters">Emitters that run once per translated type declaration.</param>
    /// <param name="MemberEmitters">Emitters that run once per translated member.</param>
    [Experimental(ExperimentalApiIds.Emitters, UrlFormat = ExperimentalApiIds.UrlFormat)]
    public void Deconstruct(out NodeTranslatorRegistry Nodes, out IReadOnlyList<IClassEmitter> ClassEmitters, out IReadOnlyList<IMemberEmitter> MemberEmitters)
    {
        Nodes = this.Nodes;
        ClassEmitters = this.ClassEmitters;
        MemberEmitters = this.MemberEmitters;
    }

    /// <summary>
    /// The built-in node translators and no emitters.
    /// </summary>
    public static TranslationEnvironment BuiltIn { get; } = new(NodeTranslatorRegistry.BuiltIn, [], []);
}
