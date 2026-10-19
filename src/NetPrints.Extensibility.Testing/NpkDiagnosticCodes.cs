namespace NetPrints.Extensibility.Testing;

/// <summary>
/// The codes of the conformance checks (analyzer-diagnostics.md §6). A failed check reports its code, the rule it enforces
/// and the offending item.
/// </summary>
public static class NpkDiagnosticCodes
{
    /// <summary>The manifest is valid, its id is well formed and its <c>netprintsApi</c> is one the host implements.</summary>
    public const string Manifest = "NPK001";

    /// <summary>The extension folder has the loader's layout and holds no copy of a host assembly.</summary>
    public const string Packaging = "NPK002";

    /// <summary>The extension's contracts resolve to the host's assemblies, not to private copies.</summary>
    public const string TypeIdentity = "NPK003";

    /// <summary>Registration is pure and repeatable: registering twice gives the same registry.</summary>
    public const string Registration = "NPK004";

    /// <summary>The extension loads and its contributions produce no registry issue.</summary>
    public const string RegistryIssues = "NPK005";

    /// <summary>Every node kind of the extension is saved and loaded byte-identically.</summary>
    public const string NodeRoundTrip = "NPK006";

    /// <summary>The C# generated for each node kind compiles.</summary>
    public const string TranslationCompiles = "NPK007";

    /// <summary>Two translations of the same graph produce identical text.</summary>
    public const string DeterministicEmitters = "NPK008";

    /// <summary>The checks implemented by this version, in order.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Manifest,
        Packaging,
        TypeIdentity,
        Registration,
        RegistryIssues,
        NodeRoundTrip,
        TranslationCompiles,
        DeterministicEmitters,
    ];
}
