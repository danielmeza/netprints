namespace NetPrints.Editor.ClassEditor;

/// <summary>
/// Sent by <see cref="NetPrints.Editor.ErrorList.ErrorListViewModel"/> when a navigable diagnostic row is
/// double-clicked or activated from the keyboard (FR-034, ED-T03): received by the owning
/// <see cref="ClassEditorViewModel"/>, which opens the graph <see cref="GraphKey"/> resolves to (if it is
/// not already open) and, when <see cref="NodeId"/> is known, reveals it through
/// <see cref="Graph.NodeGraphViewModel.RevealNode(string)"/>. A diagnostic with no node mapping still opens
/// the graph (OWN-04): <see cref="NodeId"/> is <see langword="null"/> in that case.
/// </summary>
/// <param name="GraphKey">Graph key (<see cref="Core.GraphKeys.For"/>) of the graph the node belongs to.</param>
/// <param name="NodeId">Id of the node to reveal, or <see langword="null"/> if the diagnostic has no node mapping.</param>
/// <param name="ClassFullName">Full name of the class owning the graph when the sender lists several classes (the Errors panel), or <see langword="null"/> for the sender's own class.</param>
public sealed record NavigateToNodeMessage(string GraphKey, string? NodeId, string? ClassFullName = null);
