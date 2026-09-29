#nullable enable
using System.Collections.Generic;

namespace NetPrints.Translator;

/// <summary>
/// Everything a <see cref="ClassTranslator"/> takes from extensions (extension-points.md §2.1, §3): node
/// translators and the class and member emitters, each list in registry order.
/// </summary>
/// <param name="Nodes">Node translators by node type.</param>
/// <param name="ClassEmitters">Emitters that run once per translated type declaration.</param>
/// <param name="MemberEmitters">Emitters that run once per translated member.</param>
public sealed record TranslationEnvironment(
    NodeTranslatorRegistry Nodes,
    IReadOnlyList<IClassEmitter> ClassEmitters,
    IReadOnlyList<IMemberEmitter> MemberEmitters)
{
    /// <summary>
    /// The built-in node translators and no emitters.
    /// </summary>
    public static TranslationEnvironment BuiltIn { get; } = new(NodeTranslatorRegistry.BuiltIn, [], []);
}
