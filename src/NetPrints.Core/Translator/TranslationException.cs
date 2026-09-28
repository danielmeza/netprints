#nullable enable
using System;

namespace NetPrints.Translator;

/// <summary>
/// A failure translating a graph or class to C#, carrying a stable <c>NPTnnn</c> code
/// (compilation-and-diagnostics.md §2): <c>NPT005</c> emitter failure, <c>NPT006</c> no translator for a node
/// type, <c>NPT007</c> invalid emitter output, <c>NPT008</c> a required input pin is unset.
/// </summary>
public sealed class TranslationException : Exception
{
    /// <summary>
    /// Creates an exception.
    /// </summary>
    /// <param name="code">Stable diagnostic code.</param>
    /// <param name="message">Human-readable description.</param>
    /// <param name="graphKey">Key of the graph the failure belongs to, if known.</param>
    /// <param name="nodeId">Id of the node the failure belongs to, if known.</param>
    /// <param name="inner">The exception that caused this one, if any.</param>
    public TranslationException(string code, string message, string? graphKey = null, string? nodeId = null, Exception? inner = null)
        : base(message, inner)
    {
        Code = code;
        GraphKey = graphKey;
        NodeId = nodeId;
    }

    /// <summary>
    /// Stable diagnostic code, for example <c>"NPT006"</c>.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Key of the graph the failure belongs to, or <see langword="null"/>.
    /// </summary>
    public string? GraphKey { get; }

    /// <summary>
    /// Id of the node the failure belongs to, or <see langword="null"/>.
    /// </summary>
    public string? NodeId { get; }
}
