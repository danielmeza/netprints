namespace NetPrints.Extensibility.Loading;

/// <summary>
/// Editor-side holder of the current <see cref="ExtensionRegistry"/>: rebuilds it when a trusted project adds
/// extension folders. Load contexts are cached by manifest path (not collectible), so re-loading never loads an
/// assembly twice (extension-points.md §8).
/// </summary>
public interface IExtensionHost
{
    /// <summary>
    /// The registry in use.
    /// </summary>
    ExtensionRegistry Current { get; }

    /// <summary>
    /// Rebuilds <see cref="Current"/> with the extension folders of the open project added to the ones the host was
    /// created with. Does nothing when the folders are the ones already loaded.
    /// </summary>
    /// <param name="projectExtensionFolders">The trusted project's extension folders; empty to drop the previous project's.</param>
    /// <param name="cancellationToken">Cancels the load; <see cref="Current"/> stays as it was.</param>
    /// <returns>The registry now in use.</returns>
    ExtensionRegistry LoadForProject(IReadOnlyList<string> projectExtensionFolders, CancellationToken cancellationToken);

    /// <summary>
    /// Raised, after <see cref="Current"/> changed, with the new registry. The previous registry is disposed when the
    /// handlers return.
    /// </summary>
    event EventHandler<ExtensionRegistry>? RegistryChanged;
}
