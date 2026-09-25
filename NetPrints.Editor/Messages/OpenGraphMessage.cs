using NetPrints.Core;

namespace NetPrints.Editor.Messages;

/// <summary>
/// Requests that a graph is opened in the class editor.
/// </summary>
public sealed record OpenGraphMessage(NodeGraph Graph);
