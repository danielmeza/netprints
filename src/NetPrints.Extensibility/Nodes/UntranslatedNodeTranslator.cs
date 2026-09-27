using NetPrints.Core;
using NetPrints.Graph;
using NetPrints.Translator;

namespace NetPrints.Extensibility.Nodes;

/// <summary>
/// The translator of a built-in kind the execution translator never translates (type-graph nodes and the
/// constructor entry). It fails like a missing translator does: <c>NPT006</c>.
/// </summary>
internal sealed class UntranslatedNodeTranslator : INodeTranslator
{
    public static UntranslatedNodeTranslator Instance { get; } = new();

    public void Translate(IExecutionTranslationContext context, Node node, int inputExecPinIndex)
    {
        string? graphKey;
        try
        {
            graphKey = GraphKeys.For(node.Graph);
        }
        catch (InvalidOperationException)
        {
            graphKey = null;
        }

        throw new TranslationException(TranslationDiagnosticCodes.NoTranslatorForNode, $"No translator for {node.GetType()}", graphKey, node.Id);
    }
}
