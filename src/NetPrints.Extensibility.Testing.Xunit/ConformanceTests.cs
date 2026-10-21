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
    private const string InProcessId = "harness.inprocess.0";

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
            SourceDirectory = SourceDirectory ?? manifestFolder,
            HarnessFactory = cancellationToken => CreateHarnessAsync(manifestFolder, [], cancellationToken),
            NeighbourHarnessFactory = (neighbours, cancellationToken) => CreateHarnessAsync(manifestFolder, neighbours, cancellationToken),
        };
    }

    /// <summary>
    /// Gets the directory that holds the extension's <c>.axaml</c> sources, or <see langword="null"/> to use the folder that holds
    /// <c>netprints-extension.json</c> next to the extension assembly or above it. The contributed views check scans it.
    /// </summary>
    protected virtual string? SourceDirectory => null;

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

    /// <summary>Checks <c>NPK009</c>: the settings sections.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK009_SettingsAsync() => RunCheckAsync(NpkDiagnosticCodes.Settings);

    /// <summary>Checks <c>NPK010</c>: the host channel lifecycle.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK010_HostChannelAsync() => RunCheckAsync(NpkDiagnosticCodes.HostChannel);

    /// <summary>Checks <c>NPK011</c>: coexistence with the built-in extension and a noisy neighbour.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK011_CoexistenceAsync() => RunCheckAsync(NpkDiagnosticCodes.Coexistence);

    /// <summary>Checks <c>NPK012</c>: disposal.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK012_DisposalAsync() => RunCheckAsync(NpkDiagnosticCodes.Disposal);

    /// <summary>Checks <c>NPK013</c>: the service provider.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK013_ServicesAsync() => RunCheckAsync(NpkDiagnosticCodes.Services);

    /// <summary>Checks <c>NPK015</c>: the editor contributions.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK015_EditorContributionsAsync() => RunCheckAsync(NpkDiagnosticCodes.EditorContributions);

    /// <summary>Checks <c>NPK016</c>: the contributed views.</summary>
    /// <returns>A task that completes when the check ran.</returns>
    [Fact]
    public Task NPK016_ContributedViewsAsync() => RunCheckAsync(NpkDiagnosticCodes.ContributedViews);

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

    private static async Task<ExtensionHarness> CreateHarnessAsync(
        string? folder, IReadOnlyList<(ExtensionManifest Manifest, INetPrintsExtension Extension)> neighbours, CancellationToken cancellationToken)
    {
        if (folder is null)
        {
            var subject = (new ExtensionManifest(InProcessId, InProcessId, "1.0.0", InProcessId + ".dll", $"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}", []), (INetPrintsExtension)new TExtension());
            return await ExtensionHarness.CreateWithManifestsAsync([], [subject, .. neighbours], cancellationToken).ConfigureAwait(false);
        }

        string path = Path.Combine(folder, ExtensionManifest.FileName);
        ExtensionManifest manifest;
        using (FileStream stream = File.OpenRead(path))
        {
            manifest = ExtensionManifest.Parse(stream, path);
        }

        return await ExtensionHarness.CreateWithManifestsAsync([], [(manifest, new TExtension()), .. neighbours], cancellationToken).ConfigureAwait(false);
    }

    private async Task RunCheckAsync(string code)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        ConformanceOptions options = CreateOptions();
        Func<CancellationToken, Task<ExtensionHarness>> factory = options.HarnessFactory ?? (token => CreateHarnessAsync(options.ExtensionFolder, [], token));
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
