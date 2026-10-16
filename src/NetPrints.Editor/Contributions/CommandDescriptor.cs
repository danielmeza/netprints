using NetPrints.Core;

namespace NetPrints.Editor.Contributions;

/// <summary>A command: what it is called, who runs it, and where its gestures and menu entry go.</summary>
/// <param name="Id">The namespaced id, matching <see cref="ContributionIds.IsValid"/>.</param>
/// <param name="Label">The non-empty display label. A plain string is a literal that takes the convention key <c>Command.&lt;id&gt;.Label</c> and shows itself when no resource has the key.</param>
/// <param name="Handler">Decides whether the command can run and runs it.</param>
/// <param name="IconId">Icon id (see IconIds), or null.</param>
/// <param name="DefaultGestures">The default gestures; text such as <c>Ctrl+Shift+B</c> is parsed with <see cref="CommandGesture.Parse(string, IFormatProvider?)"/> at the boundary.</param>
/// <param name="Scope">Where the gestures are active.</param>
/// <param name="Menu">The menu entry, or null for a menu-less command.</param>
/// <param name="CommandBarOrder">The position in the command bar, or null to stay off it.</param>
public sealed record CommandDescriptor(
    string Id,
    LocalizedText Label,
    ICommandHandler Handler,
    string? IconId = null,
    IReadOnlyList<CommandGesture>? DefaultGestures = null,
    CommandScope Scope = CommandScope.Global,
    MenuPlacement? Menu = null,
    int? CommandBarOrder = null)
{
    /// <summary>Gets the label with its convention key (FR-027): a literal label is keyed <c>Command.&lt;id&gt;.Label</c>.</summary>
    public LocalizedText Label { get; init; } = ConventionKeys.Command(Id, Label);

    /// <summary>Gets the label as the editor shows it: the translation for the UI culture, the default text, or the key.</summary>
    public string DisplayLabel => Label.Resolve(Strings.ResourceManager, null);
}
