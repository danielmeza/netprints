using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;

namespace NetPrints.Extensibility;

/// <summary>
/// The extension that contributes what ships with NetPrints: <see cref="BuiltInNodeLibrary"/>. Loaded in-process
/// and first (extension-points.md §8.1).
/// </summary>
public sealed class BuiltInExtension : INetPrintsExtension
{
    /// <summary>
    /// The manifest of the built-in extension; its id is <see cref="BuiltInNodeLibrary.Id"/>.
    /// </summary>
    public static ExtensionManifest Manifest { get; } = new(
        BuiltInNodeLibrary.Id, "NetPrints built-in nodes", "1.0.0", string.Empty, "1.0", []);

    /// <summary>
    /// The manifest and an instance, as an <see cref="ExtensionLoaderOptions.InProcess"/> entry.
    /// </summary>
    public static (ExtensionManifest Manifest, INetPrintsExtension Extension) InProcessEntry { get; } = (Manifest, new BuiltInExtension());

    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.AddNodeLibrary(BuiltInNodeLibrary.Instance);
    }
}
