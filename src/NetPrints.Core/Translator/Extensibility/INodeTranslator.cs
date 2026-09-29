#nullable enable
using NetPrints.Graph;

namespace NetPrints.Translator;

/// <summary>
/// Translates one node type into C# (extension-points.md §2.1). Implementations must be stateless:
/// one instance serves every translation, possibly on several threads.
/// </summary>
public interface INodeTranslator
{
    /// <summary>
    /// Emits the C# for <paramref name="node"/> through <paramref name="context"/>.
    /// </summary>
    /// <param name="context">The translation in progress.</param>
    /// <param name="node">The node to translate.</param>
    /// <param name="inputExecPinIndex">Exec nodes: called once per input exec pin (0 to
    /// <c>InputExecPins.Count - 1</c>). Pure nodes: called once with 0 and must assign every output data pin
    /// they produce.</param>
    void Translate(IExecutionTranslationContext context, Node node, int inputExecPinIndex);
}
