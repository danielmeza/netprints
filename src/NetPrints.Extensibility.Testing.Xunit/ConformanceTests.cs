using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Testing;
using Xunit;

namespace NetPrints.Extensibility.Testing.Xunit;

/// <summary>
/// One test per conformance check of the kit. Derive a public class and the checks run against <typeparamref name="TExtension"/>.
/// </summary>
/// <typeparam name="TExtension">The extension under test; created in the test process.</typeparam>
public abstract class ConformanceTests<TExtension>
    where TExtension : INetPrintsExtension, new()
{
    private const int FolderSearchDepth = 4;

    /// <summary>
    /// Gets the folder the extension is built into: the one with <c>netprints-extension.json</c> and the extension assembly, and none of the host's.
    /// The manifest, packaging and type identity checks read it and are skipped while it is <see langword="null"/>, which is the default.
    /// </summary>
    protected virtual string? ExtensionFolder => null;

    /// <summary>
    /// Creates the options the checks run with. <typeparamref name="TExtension"/> is loaded in-process under the manifest of
    /// <see cref="ExtensionFolder"/>, or under the first <c>netprints-extension.json</c> next to the extension assembly or above it.
    /// </summary>
    /// <returns>The options.</returns>
    protected virtual ConformanceOptions CreateOptions()
    {
        string? folder = ExtensionFolder;
        string? manifestFolder = folder ?? FindManifestFolder();
        return new ConformanceOptions
        {
            ExtensionFolder = folder,
            HarnessFactory = cancellationToken => CreateHarnessAsync(manifestFolder, cancellationToken),
        };
    }

    /// <summary>Checks <c>NPK001</c>: the manifest.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK001_ManifestAsync() => RunCheckAsync(NpkDiagnosticCodes.Manifest);

    /// <summary>Checks <c>NPK002</c>: the packaging.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK002_PackagingAsync() => RunCheckAsync(NpkDiagnosticCodes.Packaging);

    /// <summary>Checks <c>NPK003</c>: the type identity.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK003_TypeIdentityAsync() => RunCheckAsync(NpkDiagnosticCodes.TypeIdentity);

    /// <summary>Checks <c>NPK004</c>: pure and repeatable registration.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK004_RegistrationAsync() => RunCheckAsync(NpkDiagnosticCodes.Registration);

    /// <summary>Checks <c>NPK005</c>: no registry issues.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK005_RegistryIssuesAsync() => RunCheckAsync(NpkDiagnosticCodes.RegistryIssues);

    /// <summary>Checks <c>NPK006</c>: the node round trip.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK006_NodeRoundTripAsync() => RunCheckAsync(NpkDiagnosticCodes.NodeRoundTrip);

    /// <summary>Checks <c>NPK007</c>: the generated C# compiles.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK007_TranslationCompilesAsync() => RunCheckAsync(NpkDiagnosticCodes.TranslationCompiles);

    /// <summary>Checks <c>NPK008</c>: deterministic emitters.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK008_DeterministicEmittersAsync() => RunCheckAsync(NpkDiagnosticCodes.DeterministicEmitters);

    private static string? FindManifestFolder()
    {
        string? directory = Path.GetDirectoryName(typeof(TExtension).Assembly.Location);
        for (int depth = 0; depth < FolderSearchDepth && directory is not null; depth++)
        {
            if (File.Exists(Path.Combine(directory, ExtensionManifest.FileName)))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    private static async Task<ExtensionHarness> CreateHarnessAsync(string? folder, CancellationToken cancellationToken)
    {
        if (folder is null)
        {
            return await ExtensionHarness.CreateAsync([], [new TExtension()], cancellationToken).ConfigureAwait(false);
        }

        string path = Path.Combine(folder, ExtensionManifest.FileName);
        ExtensionManifest manifest;
        using (FileStream stream = File.OpenRead(path))
        {
            manifest = ExtensionManifest.Parse(stream, path);
        }

        return await ExtensionHarness.CreateWithManifestsAsync([], [(manifest, new TExtension())], cancellationToken).ConfigureAwait(false);
    }

    private async Task RunCheckAsync(string code)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ConformanceOptions options = CreateOptions();
        Func<CancellationToken, Task<ExtensionHarness>> factory = options.HarnessFactory ?? (token => CreateHarnessAsync(options.ExtensionFolder, token));
        await using ExtensionHarness harness = await factory(cancellationToken).ConfigureAwait(false);
        ConformanceCheckResult result = await ConformanceSuite.RunCheckAsync(harness, options, code, cancellationToken).ConfigureAwait(false);
        switch (result.Status)
        {
            case ConformanceStatus.Skipped:
                Assert.Skip($"{result.Code} {result.Name}: {result.Note}");
                break;
            case ConformanceStatus.Failed:
                Assert.Fail(string.Join(Environment.NewLine, result.Findings.Select(finding => finding.ToString())));
                break;
            case ConformanceStatus.Passed:
                break;
        }
    }
}
