namespace NetPrints.Extensibility.Loading;

/// <summary>
/// The outcome of loading one extension (extension-points.md §8).
/// </summary>
/// <param name="Id">The extension id; for a manifest that could not be read, the name of its folder.</param>
/// <param name="ManifestPath">Full path of the manifest, or <see langword="null"/> for an in-process extension.</param>
public abstract record ExtensionLoadResult(string Id, string? ManifestPath)
{
    /// <summary>
    /// The extension loaded and its contributions are in the registry.
    /// </summary>
    /// <param name="Id">The extension id.</param>
    /// <param name="ManifestPath">Full path of the manifest, or <see langword="null"/> for an in-process extension.</param>
    /// <param name="Manifest">The manifest.</param>
    public sealed record Loaded(string Id, string? ManifestPath, ExtensionManifest Manifest) : ExtensionLoadResult(Id, ManifestPath);

    /// <summary>
    /// The extension did not load.
    /// </summary>
    /// <param name="Id">The extension id; for a manifest that could not be read, the name of its folder.</param>
    /// <param name="ManifestPath">Full path of the manifest, or <see langword="null"/> for an in-process extension.</param>
    /// <param name="Code">The <c>NPX</c> code, see <see cref="ExtensionDiagnosticCodes"/>.</param>
    /// <param name="Reason">Human-readable reason.</param>
    /// <param name="Exception">The exception behind the failure, if any.</param>
    public sealed record Failed(string Id, string? ManifestPath, string Code, string Reason, Exception? Exception) : ExtensionLoadResult(Id, ManifestPath);
}
