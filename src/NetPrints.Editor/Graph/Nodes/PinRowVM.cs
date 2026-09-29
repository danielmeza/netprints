using NetPrints.Editor.Graph.Pins;

namespace NetPrints.Editor.Graph.Nodes;

/// <summary>
/// One row of a node's pin area (OWN-05b): an optional pin in each column, rendered on the same
/// row so pins that belong together (a parameter's type and value, or a return value's data and
/// type pin) share a vertical center, regardless of where each one sits in the node's own pin
/// lists.
/// </summary>
/// <param name="Left">Pin in the row's left column, or <see langword="null"/> if the row has none.</param>
/// <param name="Right">Pin in the row's right column, or <see langword="null"/> if the row has none.</param>
public sealed record PinRowVM(NodePinVM? Left, NodePinVM? Right);
