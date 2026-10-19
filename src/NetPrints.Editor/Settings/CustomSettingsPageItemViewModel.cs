namespace NetPrints.Editor.Settings;

/// <summary>
/// An extension's own settings page (ADR-0040 decision 4): the view model its factory made, shown through the extension's view
/// resources; a factory that threw gives the <see cref="Contributions.ContributionErrorViewModel"/> placeholder instead.
/// </summary>
/// <param name="label">The page title, used by the search.</param>
/// <param name="content">The view model the page shows.</param>
internal sealed class CustomSettingsPageItemViewModel(string label, object content) : SettingsItemViewModel(label, ""), IDisposable
{
    /// <summary>Gets the view model the page shows.</summary>
    public object Content { get; } = content;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Content is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}
