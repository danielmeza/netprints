namespace NetPrints.Editor.ClassEditor;

/// <summary>
/// Sent by <see cref="NetPrints.Editor.ErrorList.ErrorListVM"/> when a navigable diagnostic row is
/// double-clicked (FR-034, ED-T03): received by the owning <see cref="ClassEditorVM"/>, which opens
/// the graph <see cref="GraphKey"/> resolves to (if it is not already open) and reveals
/// <see cref="NodeId"/> through <see cref="Graph.NodeGraphVM.RevealNode(string)"/>.
/// </summary>
/// <param name="GraphKey">Graph key (<see cref="Core.GraphKeys.For"/>) of the graph the node belongs to.</param>
/// <param name="NodeId">Id of the node to reveal.</param>
public sealed record NavigateToNodeMessage(string GraphKey, string NodeId);
