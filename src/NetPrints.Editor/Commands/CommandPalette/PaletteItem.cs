using NetPrints.Editor.Contributions;

namespace NetPrints.Editor.Commands.CommandPalette;

/// <summary>One command in the command palette.</summary>
/// <param name="Command">The registered command.</param>
/// <param name="Label">The command's label.</param>
/// <param name="Shortcuts">The default shortcuts as written, separated by <c>, </c>; empty when it has none.</param>
/// <param name="MenuPath">The menu the command is in, such as <c>View</c>; empty when it has no menu entry.</param>
/// <param name="IsEnabled">Whether the command could run when the list was built; a disabled command is shown but Enter does not run it.</param>
public sealed record PaletteItem(CommandDescriptor Command, string Label, string Shortcuts, string MenuPath, bool IsEnabled);
