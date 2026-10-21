namespace NetPrints.Editor.Contributions;

/// <summary>Stands in for the view model of an extension's contribution whose factory threw: the surface shows <see cref="Title"/> and the rest renders as usual.</summary>
public sealed class ContributionErrorViewModel
{
    /// <summary>Creates the placeholder.</summary>
    /// <param name="extensionName">The display name of the extension.</param>
    /// <param name="contribution">What failed, such as <c>panel fx.faulty.panel</c>.</param>
    /// <param name="message">The message of the exception the extension threw.</param>
    public ContributionErrorViewModel(string extensionName, string contribution, string message)
    {
        ExtensionName = extensionName;
        Title = TextFormat.Of(Strings.ContributionError_Title, extensionName, contribution);
        Message = message;
    }

    /// <summary>Gets the display name of the extension.</summary>
    public string ExtensionName { get; }

    /// <summary>Gets the sentence that names the extension and what it could not show.</summary>
    public string Title { get; }

    /// <summary>Gets the message of the exception the extension threw.</summary>
    public string Message { get; }
}
