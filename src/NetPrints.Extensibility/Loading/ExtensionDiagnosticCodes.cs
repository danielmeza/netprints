namespace NetPrints.Extensibility.Loading;

/// <summary>
/// The stable <c>NPX</c> codes of extension loading problems (compilation-and-diagnostics.md, extension-points.md §8).
/// </summary>
public static class ExtensionDiagnosticCodes
{
    /// <summary>Invalid manifest, missing manifest, or an assembly without exactly one usable extension type.</summary>
    public const string InvalidManifest = "NPX001";

    /// <summary>The manifest asks for an incompatible <c>netprintsApi</c> version.</summary>
    public const string ApiVersion = "NPX002";

    /// <summary>A dependency is missing, failed to load, or is part of a dependency cycle.</summary>
    public const string Dependency = "NPX003";

    /// <summary>Another extension with the same id was found first.</summary>
    public const string DuplicateId = "NPX004";

    /// <summary>Creating the extension or its <see cref="INetPrintsExtension.Register"/> threw.</summary>
    public const string RegisterFailed = "NPX005";

    /// <summary>A contribution was rejected: conflict or inconsistency. The extension stays loaded.</summary>
    public const string ContributionRejected = "NPX006";

    /// <summary>The extension assembly is missing or could not be loaded.</summary>
    public const string AssemblyLoadFailed = "NPX007";
}
