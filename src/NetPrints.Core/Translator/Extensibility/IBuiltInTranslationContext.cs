#nullable enable
using NetPrints.Graph;

namespace NetPrints.Translator;

/// <summary>
/// The part of <see cref="ExecutionGraphTranslator"/>'s state only the built-in node translators need.
/// </summary>
internal interface IBuiltInTranslationContext : IExecutionTranslationContext
{
    /// <summary>
    /// Whether <paramref name="pin"/>'s state is the last one before the jump-stack state, so a bare
    /// <c>return;</c> would fall off the end of the method anyway.
    /// </summary>
    /// <param name="pin">Input exec pin of the node being translated.</param>
    /// <returns><see langword="true"/> for the final state.</returns>
    bool IsFinalExecState(NodeInputExecPin pin);
}
