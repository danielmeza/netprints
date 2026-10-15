namespace NetPrints.Editor.Shell;

/// <summary>The project tree is asked to rename a model object in place (<see cref="ShellViewModel.RequestInlineRename"/>).</summary>
/// <param name="item">The method, variable or event graph to rename.</param>
public sealed class InlineRenameRequestedEventArgs(object item) : EventArgs
{
    /// <summary>Gets the model object to rename.</summary>
    public object Item { get; } = item;

    /// <summary>Gets or sets a value indicating whether a row took the request.</summary>
    public bool Handled { get; set; }
}
