namespace NetPrints.Editor.Commands.KeyboardShortcuts;

/// <summary>One command in the keyboard shortcuts sheet.</summary>
/// <param name="CommandId">The registered command id.</param>
/// <param name="Label">The command's label as registered; a dynamic label such as <c>Undo Add node</c> is not used.</param>
/// <param name="Shortcuts">The default shortcuts as written, separated by <c>, </c>; empty when it has none.</param>
public sealed record ShortcutRow(string CommandId, string Label, string Shortcuts);

/// <summary>A titled block of the keyboard shortcuts sheet: one menu, or the commands that have no menu.</summary>
/// <param name="Title">The menu name, or <see cref="KeyboardShortcutsViewModel.OtherTitle"/>.</param>
/// <param name="Rows">The commands, in menu order.</param>
public sealed record ShortcutGroup(string Title, IReadOnlyList<ShortcutRow> Rows);
