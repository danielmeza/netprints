using Microsoft.Extensions.Time.Testing;

namespace NetPrints.Desktop.E2ETests.Hosting;

/// <summary>
/// The rules of <see cref="FailureCapture"/> (contracts/ci.md §3), run unconditionally over fake
/// parts and a fake clock: no display or editor is needed.
/// </summary>
public sealed class FailureCaptureTests : IDisposable
{
    private static readonly TimeSpan Real = TimeSpan.FromSeconds(10);

    private readonly string directory = Directory.CreateTempSubdirectory("netprints-capture-").FullName;
    private readonly FakeTimeProvider clock = new();

    private static CapturePart Writes(string name) =>
        new(name, name + ".txt", (path, token) => File.WriteAllTextAsync(path, name, token));

    private static CapturePart Throws(string name) =>
        new(name, name + ".txt", (_, _) => throw new InvalidOperationException("boom"));

    private static CapturePart Hangs(string name) =>
        new(name, name + ".txt", (_, _) => new TaskCompletionSource().Task);

    [Fact]
    public async Task AThrowingPartIsNamedAndTheOthersAreStillWritten()
    {
        var capture = new FailureCapture(clock, [Writes("first"), Throws("second"), Writes("third")]);

        await capture.CaptureAsync(directory, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(directory, "first.txt")));
        Assert.True(File.Exists(Path.Combine(directory, "third.txt")));
        string errors = await File.ReadAllTextAsync(Path.Combine(directory, FailureCapture.ErrorsFileName), TestContext.Current.CancellationToken);
        Assert.Contains("second", errors, StringComparison.Ordinal);
        Assert.Contains("boom", errors, StringComparison.Ordinal);
        Assert.DoesNotContain("first", errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoErrorsFileWhenEveryPartSucceeds()
    {
        var capture = new FailureCapture(clock, [Writes("first")]);

        await capture.CaptureAsync(directory, TestContext.Current.CancellationToken);

        Assert.False(File.Exists(Path.Combine(directory, FailureCapture.ErrorsFileName)));
    }

    [Fact]
    public async Task AHangingPartIsAbandonedAfterTenSecondsAndNamed()
    {
        var capture = new FailureCapture(clock, [Writes("first"), Hangs("stuck")]);
        var running = capture.CaptureAsync(directory, TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(9));
        Assert.False(running.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(1));
        await running.WaitAsync(Real, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(directory, "first.txt")));
        string errors = await File.ReadAllTextAsync(Path.Combine(directory, FailureCapture.ErrorsFileName), TestContext.Current.CancellationToken);
        Assert.Contains("stuck", errors, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheWholeCaptureEndsWithinThirtySecondsEvenWhenEveryPartHangs()
    {
        var capture = new FailureCapture(clock, [Hangs("a"), Hangs("b"), Hangs("c"), Hangs("d"), Hangs("e"), Hangs("f"), Hangs("g")]);
        var started = clock.GetUtcNow();
        var running = capture.CaptureAsync(directory, TestContext.Current.CancellationToken);

        clock.Advance(FailureCapture.PartLimit);
        await running.WaitAsync(Real, TestContext.Current.CancellationToken);

        Assert.True(clock.GetUtcNow() - started <= TimeSpan.FromSeconds(30));
        string errors = await File.ReadAllTextAsync(Path.Combine(directory, FailureCapture.ErrorsFileName), TestContext.Current.CancellationToken);
        Assert.Equal(7, errors.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public async Task TheTotalLimitCutsOffPartsWithALongerOwnLimit()
    {
        var capture = new FailureCapture(clock, [Hangs("slow")], partLimit: TimeSpan.FromMinutes(5));
        var running = capture.CaptureAsync(directory, TestContext.Current.CancellationToken);

        clock.Advance(TimeSpan.FromSeconds(29));
        Assert.False(running.IsCompleted);

        clock.Advance(TimeSpan.FromSeconds(1));
        await running.WaitAsync(Real, TestContext.Current.CancellationToken);

        string errors = await File.ReadAllTextAsync(Path.Combine(directory, FailureCapture.ErrorsFileName), TestContext.Current.CancellationToken);
        Assert.Contains("slow", errors, StringComparison.Ordinal);
    }

    [Fact]
    public void TheStepFailureCarriesTheStepAndTheOriginalUnchanged()
    {
        var original = new InvalidOperationException("the original");

        var failure = new E2EStepFailureException("compile", TimeSpan.FromSeconds(12.4), original);

        Assert.StartsWith("[step 'compile' running for 12 s]", failure.Message, StringComparison.Ordinal);
        Assert.Contains("the original", failure.Message, StringComparison.Ordinal);
        Assert.Same(original, failure.InnerException);
    }

    [Fact]
    public async Task ACaptureFailureNeverReplacesTheOriginal()
    {
        string blocker = Path.Combine(directory, "file");
        await File.WriteAllTextAsync(blocker, "x", TestContext.Current.CancellationToken);
        var capture = new FailureCapture(clock, [Writes("first")]);
        var original = new InvalidOperationException("the original");

        var failure = await capture.FailAsync(original, "run", TimeSpan.FromSeconds(3), Path.Combine(blocker, "inside"), TestContext.Current.CancellationToken);

        Assert.Same(original, failure.InnerException);
        Assert.StartsWith("[step 'run' running for 3 s]", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FailAsyncCapturesThenReportsTheOriginal()
    {
        var capture = new FailureCapture(clock, [Writes("first")]);
        var original = new InvalidOperationException("the original");

        var failure = await capture.FailAsync(original, null, TimeSpan.Zero, directory, TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(directory, "first.txt")));
        Assert.Same(original, failure.InnerException);
    }

    public void Dispose() => Directory.Delete(directory, recursive: true);
}
