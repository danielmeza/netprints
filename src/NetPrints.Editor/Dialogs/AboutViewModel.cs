namespace NetPrints.Editor.Dialogs;

/// <summary>A titled link of the About dialog.</summary>
/// <param name="Title">What the link is.</param>
/// <param name="Url">The address, shown as text.</param>
public sealed record AboutLink(string Title, string Url);

/// <summary>The About dialog: the editor's version and the project links.</summary>
public sealed class AboutViewModel
{
    /// <summary>Creates the dialog's content.</summary>
    /// <param name="version">The editor version.</param>
    public AboutViewModel(string version)
    {
        Version = version;
        Links =
        [
            new AboutLink("Source code", "https://github.com/danielmeza/netprints"),
            new AboutLink("Documentation", "https://danielmeza.github.io/netprints/"),
            new AboutLink("Releases", "https://github.com/danielmeza/netprints/releases"),
            new AboutLink("Original NetPrints by Robin Kahlow", "https://github.com/RobinKa/netprints"),
        ];
    }

    /// <summary>Gets the editor version.</summary>
    public string Version { get; }

    /// <summary>Gets the project links.</summary>
    public IReadOnlyList<AboutLink> Links { get; }
}
