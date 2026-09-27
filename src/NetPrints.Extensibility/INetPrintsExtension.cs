namespace NetPrints.Extensibility;

/// <summary>
/// The entry point of an extension (extension-points.md §1). An extension assembly has exactly one public,
/// non-abstract implementation with a public parameterless constructor.
/// </summary>
public interface INetPrintsExtension
{
    /// <summary>
    /// Declares the extension's contributions through <paramref name="builder"/>. Called once, on the loading
    /// thread, before the registry is built. The contributions are committed only if this method returns
    /// normally; an exception discards all of them (<c>NPX005</c>).
    /// </summary>
    /// <param name="builder">The buffered builder for this extension.</param>
    void Register(IExtensionBuilder builder);
}
