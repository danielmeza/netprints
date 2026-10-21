using NetPrints.Extensibility.Loading;

namespace NetPrints.Extensibility.Testing;

/// <summary>
/// What <see cref="ExtensionTest{TExtension}.RunAsync"/> produced. The test that ran it owns the harness.
/// </summary>
public sealed class ExtensionTestResult
{
    internal ExtensionTestResult(ExtensionHarness harness, string? translatedCode, byte[]? document, byte[]? roundTripped)
    {
        Harness = harness;
        TranslatedCode = translatedCode;
        Document = document;
        RoundTrippedDocument = roundTripped;
    }

    /// <summary>Gets the harness the test ran against.</summary>
    public ExtensionHarness Harness { get; }

    /// <summary>Gets the registry the loader built.</summary>
    public ExtensionRegistry Registry => Harness.Registry;

    /// <summary>Gets the load results that are failures: extensions that did not load.</summary>
    public IReadOnlyList<ExtensionLoadResult.Failed> LoadFailures => [.. Registry.Results.OfType<ExtensionLoadResult.Failed>()];

    /// <summary>Gets the C# the translators and emitters produced for the declared node kinds, or <see langword="null"/> when none were declared.</summary>
    public string? TranslatedCode { get; }

    /// <summary>Gets the class document written for the declared node kinds, or <see langword="null"/> when none were declared.</summary>
    public byte[]? Document { get; }

    /// <summary>Gets the document after it was read and written again, or <see langword="null"/> when none was declared.</summary>
    public byte[]? RoundTrippedDocument { get; }

}
