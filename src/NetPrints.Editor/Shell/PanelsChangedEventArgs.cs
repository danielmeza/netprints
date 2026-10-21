namespace NetPrints.Editor.Shell;

/// <summary>The panels that joined or left <see cref="ShellViewModel.Panels"/> after a registry change.</summary>
/// <param name="added">The panels that were added, with their content created.</param>
/// <param name="removed">The panels that were removed, with their content detached.</param>
public sealed class PanelsChangedEventArgs(IReadOnlyList<PanelViewModel> added, IReadOnlyList<PanelViewModel> removed) : EventArgs
{
    /// <summary>Gets the panels that were added, with their content created.</summary>
    public IReadOnlyList<PanelViewModel> Added { get; } = added;

    /// <summary>Gets the panels that were removed, with their content detached.</summary>
    public IReadOnlyList<PanelViewModel> Removed { get; } = removed;
}
