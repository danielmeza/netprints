#nullable enable
using System;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Translator;

/// <summary>
/// What an <see cref="INodeTranslator"/> may use while a graph is being translated
/// (extension-points.md §2.1): the text sink, stable pin names, pin values and the exec-flow helpers.
/// </summary>
public interface IExecutionTranslationContext
{
    /// <summary>
    /// The graph being translated: a method, constructor or event graph.
    /// </summary>
    NodeGraph Graph { get; }

    /// <summary>
    /// The type declaration <see cref="Graph"/> belongs to, or <see langword="null"/> when the graph is not
    /// attached to one. Prefer this to <see cref="Class"/> so a translator does not assume the declaration is a
    /// class.
    /// </summary>
    ITypeDeclaration? Declaration { get; }

    /// <summary>
    /// The class <see cref="Graph"/> belongs to, or <see langword="null"/> when the graph is not attached to a
    /// class. Class-specific convenience over <see cref="Declaration"/>.
    /// </summary>
    ClassGraph? Class { get; }

    /// <summary>
    /// Appends <paramref name="code"/> to the output.
    /// </summary>
    /// <param name="code">C# text.</param>
    void Append(string code);

    /// <summary>
    /// Appends <paramref name="code"/> and a line break to the output.
    /// </summary>
    /// <param name="code">C# text; empty for a blank line.</param>
    void AppendLine(string code = "");

    /// <summary>
    /// The stable local variable name for an output pin, creating it on first use.
    /// </summary>
    /// <param name="pin">Output data pin.</param>
    /// <returns>The variable name.</returns>
    string GetOrCreatePinName(NodeOutputDataPin pin);

    /// <summary>
    /// <c>"&lt;Type&gt; &lt;name&gt;"</c> for an output pin, as used in a declaration.
    /// </summary>
    /// <param name="pin">Output data pin.</param>
    /// <returns>The type and variable name.</returns>
    string GetOrCreateTypedPinName(NodeOutputDataPin pin);

    /// <summary>
    /// The C# expression for an input pin's incoming value, or <see langword="null"/> when the argument is
    /// omitted so the parameter's own default applies.
    /// </summary>
    /// <param name="pin">Input data pin.</param>
    /// <returns>The expression, or <see langword="null"/>.</returns>
    /// <exception cref="InvalidOperationException">The pin is unconnected and has neither a value nor a default.</exception>
    string? GetPinIncomingValue(NodeInputDataPin pin);

    /// <summary>
    /// Emits every pure node <paramref name="node"/> depends on, in dependency order.
    /// </summary>
    /// <param name="node">Node whose pure dependencies are emitted.</param>
    void TranslateDependentPureNodes(Node node);

    /// <summary>
    /// Emits a jump to whatever <paramref name="pin"/> is connected to, or to the jump stack when unconnected.
    /// </summary>
    /// <param name="pin">Output exec pin to leave through.</param>
    void WriteGotoOutputPin(NodeOutputExecPin pin);

    /// <summary>
    /// Like <see cref="WriteGotoOutputPin"/> but omits the jump when the target is the state emitted right after
    /// <paramref name="fromPin"/>.
    /// </summary>
    /// <param name="pin">Output exec pin to leave through.</param>
    /// <param name="fromPin">Input exec pin whose state is being emitted.</param>
    void WriteGotoOutputPinIfNecessary(NodeOutputExecPin pin, NodeInputExecPin fromPin);

    /// <summary>
    /// Emits a push of <paramref name="pin"/>'s state onto the jump stack.
    /// </summary>
    /// <param name="pin">Input exec pin to return to later.</param>
    void WritePushJumpStack(NodeInputExecPin pin);

    /// <summary>
    /// Emits a jump to the jump stack's dispatch state.
    /// </summary>
    void WriteGotoJumpStack();

    /// <summary>
    /// A fresh temporary variable name. Deterministic: the sequence is the same for every translation.
    /// </summary>
    /// <returns>The temporary variable name.</returns>
    string CreateTemporaryVariableName();
}
