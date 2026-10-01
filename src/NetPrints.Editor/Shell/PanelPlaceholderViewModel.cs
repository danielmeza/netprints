namespace NetPrints.Editor.Shell;

/// <summary>The content of a built-in panel whose own view model has not been wired in yet: it shows a one-line empty state.</summary>
/// <param name="Title">The panel's title.</param>
public sealed record PanelPlaceholderViewModel(string Title)
{
    /// <summary>Gets the empty-state text.</summary>
    public string Message => $"Nothing to show in {Title} yet.";
}
