namespace NetPrints.Editor.Contributions;

/// <summary>A command: what it is called, who runs it, and where its gestures and menu entry go.</summary>
/// <param name="Id">The namespaced id, matching <see cref="ContributionIds.IsValid"/>.</param>
/// <param name="Label">The non-empty display label.</param>
/// <param name="Handler">Decides whether the command can run and runs it.</param>
/// <param name="IconId">Icon id (see IconIds), or null.</param>
/// <param name="DefaultGestures">Gesture strings such as <c>Ctrl+Shift+B</c>, parsed by <see cref="CommandGesture"/>.</param>
/// <param name="Scope">Where the gestures are active.</param>
/// <param name="Menu">The menu entry, or null for a menu-less command.</param>
/// <param name="CommandBarOrder">The position in the command bar, or null to stay off it.</param>
public sealed record CommandDescriptor(
    string Id,
    string Label,
    ICommandHandler Handler,
    string? IconId = null,
    IReadOnlyList<string>? DefaultGestures = null,
    CommandScope Scope = CommandScope.Global,
    MenuPlacement? Menu = null,
    int? CommandBarOrder = null);
