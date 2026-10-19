namespace NetPrints.Extensibility.Testing;

/// <summary>
/// What the conformance suite needs to know about the extension under test beyond the harness it runs against.
/// </summary>
public sealed class ConformanceOptions
{
    /// <summary>
    /// Gets the folder that holds the extension's <c>netprints-extension.json</c> and assemblies, or <see langword="null"/> when the extension
    /// is only available in-process. The manifest, packaging and type identity checks are skipped without it.
    /// </summary>
    public string? ExtensionFolder { get; init; }

    /// <summary>
    /// Gets the id of the extension under test. When <see langword="null"/>, it is the one extension that is loaded besides the built-in one.
    /// </summary>
    public string? ExtensionId { get; init; }

    /// <summary>
    /// Gets the directory that holds the extension's source files, or <see langword="null"/>. Checks that read sources use it.
    /// </summary>
    public string? SourceDirectory { get; init; }

    /// <summary>
    /// Gets a factory that loads the extension into a new harness. Checks that compare two loads need it and are skipped without it.
    /// </summary>
    public Func<CancellationToken, Task<ExtensionHarness>>? HarnessFactory { get; init; }
}
