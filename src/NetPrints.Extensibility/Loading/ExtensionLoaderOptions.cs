namespace NetPrints.Extensibility.Loading;

/// <summary>
/// What an <see cref="ExtensionLoader"/> loads (extension-points.md §8).
/// </summary>
/// <param name="SearchDirectories">Directories whose immediate subfolders may hold extensions: the settings'
/// <c>netprints.extensionPaths</c> then <c>NETPRINTS_EXTENSION_PATH</c> entries in the editor; empty in the generator.</param>
/// <param name="ExtensionFolders">Folders that must hold a <c>netprints-extension.json</c>: trusted
/// <c>NetPrintsExtension</c> items in the editor, the request's <c>extension=</c> lines in the generator.</param>
/// <param name="InProcess">Extensions loaded without an <c>AssemblyLoadContext</c>: the built-in one and tests'.</param>
public sealed record ExtensionLoaderOptions(
    IReadOnlyList<string> SearchDirectories,
    IReadOnlyList<string> ExtensionFolders,
    IReadOnlyList<(ExtensionManifest Manifest, INetPrintsExtension Extension)> InProcess)
{
    /// <summary>
    /// Only the built-in extension: no search directories and no folders.
    /// </summary>
    public static ExtensionLoaderOptions BuiltInOnly { get; } = new([], [], [BuiltInExtension.InProcessEntry]);
}
