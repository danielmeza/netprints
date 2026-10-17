namespace NetPrints.Extensibility;

/// <summary>
/// The version of the extension API this host implements (extension-points.md, header).
/// </summary>
public static class ExtensionApi
{
    private const int Minor = 4;

    /// <summary>
    /// The <c>netprintsApi</c> version. A breaking change bumps the major; an extension whose manifest asks for
    /// another major, or a newer minor, is rejected with <c>NPX002</c>. The minor changes with every shipped API change
    /// (<c>ExtensionApiVersionTests</c> ties it to the <c>PublicAPI.*.txt</c> files). Before 1.0 the major is 0.
    /// </summary>
    public static Version Version { get; } = new(0, Minor);
}
