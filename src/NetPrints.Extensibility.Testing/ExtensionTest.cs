using NetPrints.Core;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Extensibility.Testing;

/// <summary>
/// A base class for tests of one extension. Declare what to load and which node kinds to exercise in <see cref="State"/>, call
/// <see cref="RunAsync"/> and assert on the <see cref="ExtensionTestResult"/>. The harnesses a test creates are disposed with the test.
/// </summary>
/// <typeparam name="TExtension">The extension under test; created in the test process.</typeparam>
public abstract class ExtensionTest<TExtension> : IAsyncDisposable
    where TExtension : INetPrintsExtension, new()
{
    private const string TestNamespace = "ExtensionTest";
    private const string TestClassName = "Subject";

    private readonly List<ExtensionHarness> harnesses = [];

    /// <summary>Gets the declaration of what the test loads and exercises.</summary>
    protected TestState State { get; } = new();

    /// <summary>
    /// Loads <typeparamref name="TExtension"/> and the declared folders, then translates and saves a class with the declared node kinds.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the run.</param>
    /// <returns>The result; its harness is disposed when the test is.</returns>
    protected async Task<ExtensionTestResult> RunAsync(CancellationToken cancellationToken)
    {
        string api = $"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}";
        var manifest = new ExtensionManifest(State.ExtensionId, State.ExtensionId, "1.0.0", typeof(TExtension).Assembly.GetName().Name + ".dll", api, []);
        ExtensionHarness harness = await ExtensionHarness.CreateWithManifestsAsync([.. State.ExtensionFolders], [(manifest, new TExtension())], cancellationToken).ConfigureAwait(false);
        harnesses.Add(harness);
        if (State.NodeKinds.Count == 0)
        {
            return new ExtensionTestResult(harness, null, null, null);
        }

        ClassGraph graph = harness.BuildClass(TestNamespace, TestClassName, [.. State.NodeKinds]);
        string code = harness.Translate(graph);
        byte[] document = await harness.WriteAsync(graph, cancellationToken).ConfigureAwait(false);
        byte[] roundTripped = await harness.RoundTripAsync(document, cancellationToken).ConfigureAwait(false);
        return new ExtensionTestResult(harness, code, document, roundTripped);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await DisposeAsyncCore().ConfigureAwait(false);
        GC.SuppressFinalize(this);
    }

    /// <summary>Disposes the harnesses <see cref="RunAsync"/> created.</summary>
    /// <returns>A task that completes when they are disposed.</returns>
    protected virtual async ValueTask DisposeAsyncCore()
    {
        foreach (ExtensionHarness harness in harnesses)
        {
            await harness.DisposeAsync().ConfigureAwait(false);
        }

        harnesses.Clear();
    }
}
