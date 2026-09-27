namespace NetPrints.Extensibility;

/// <summary>
/// The version of the extension API this host implements (extension-points.md, header).
/// </summary>
public static class ExtensionApi
{
    /// <summary>
    /// The <c>netprintsApi</c> version. A breaking change bumps the major; an extension whose manifest asks for
    /// another major, or a newer minor, is rejected with <c>NPX002</c>.
    /// </summary>
    public static Version Version { get; } = new(1, 0);
}
