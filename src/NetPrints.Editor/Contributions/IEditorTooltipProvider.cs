namespace NetPrints.Editor.Contributions;

/// <summary>A built-in tooltip provider that may also read the editor's own object behind a target; extensions see only the model subject.</summary>
internal interface IEditorTooltipProvider : ITooltipProvider
{
    /// <summary>Gets the content of a target, with the editor's object behind it.</summary>
    /// <param name="target">The target, with its model subject.</param>
    /// <param name="editorSubject">The editor's object, such as a connection view model.</param>
    /// <returns>The content, or <see langword="null"/>.</returns>
    TooltipContent? TryProvide(TooltipTarget target, object editorSubject);
}
