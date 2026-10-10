namespace NetPrints.Editor.Contributions;

/// <summary>A context-menu entry that runs a registered command.</summary>
/// <param name="Id">The namespaced id.</param>
/// <param name="Target">The item the menu opens on.</param>
/// <param name="CommandId">The id of the command the entry runs.</param>
/// <param name="Group">The group within the menu.</param>
/// <param name="Order">The position within the group.</param>
public sealed record ContextMenuItemDescriptor(string Id, ContextMenuTarget Target, string CommandId, string Group, int Order);
