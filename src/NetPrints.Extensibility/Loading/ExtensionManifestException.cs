namespace NetPrints.Extensibility.Loading;

/// <summary>
/// A manifest that is not valid (<see cref="ExtensionDiagnosticCodes.InvalidManifest"/>).
/// </summary>
public sealed class ExtensionManifestException : Exception
{
    /// <summary>
    /// Creates the exception.
    /// </summary>
    /// <param name="manifestPath">Path of the manifest, for messages.</param>
    /// <param name="message">What is wrong with it.</param>
    /// <param name="inner">The parser exception, if any.</param>
    public ExtensionManifestException(string manifestPath, string message, Exception? inner = null)
        : base($"{manifestPath}: {message}", inner)
    {
        ManifestPath = manifestPath;
    }

    /// <summary>
    /// The stable diagnostic code, <see cref="ExtensionDiagnosticCodes.InvalidManifest"/>.
    /// </summary>
    public string Code => ExtensionDiagnosticCodes.InvalidManifest;

    /// <summary>
    /// Path of the manifest.
    /// </summary>
    public string ManifestPath { get; }
}
