using System.Text.Json;
using NetPrints.Desktop.E2ETests.Hosting;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>
/// Proves the failure diagnostics (contracts/ci.md §3) on the real editor: forces a timeout at a
/// named step of a short scenario, for this test only (never through the process environment, so
/// parallel workers are unaffected), and checks the files the capture leaves behind.
/// </summary>
public sealed class E2EDiagnosticsTests(DesktopWorkerPool pool) : X11SmokeTestBase(pool)
{
    private static readonly string[] Files =
        ["summary.md", "timings.md", "ui-tree.json", "display.png", "editor.log", "process.txt", "run-state.json"];

    protected override string? ForcedTimeoutStep => "open project";

    protected override TimeSpan Budget => TimeSpan.FromSeconds(20);

    [Fact]
    public async Task AForcedTimeoutLeavesTheDiagnosticFiles()
    {
        var failure = await Assert.ThrowsAsync<E2EStepFailureException>(() => RunScenarioAsync(EditCompileAndRunAsync));

        Assert.StartsWith("[step 'open project' running for ", failure.Message, StringComparison.Ordinal);
        Assert.IsAssignableFrom<OperationCanceledException>(failure.InnerException);

        string folder = FailureCapture.FolderFor(nameof(E2EDiagnosticsTests));
        foreach (string file in Files)
        {
            Assert.True(File.Exists(Path.Combine(folder, file)), $"{file} is missing from {folder}");
        }

        Assert.False(File.Exists(Path.Combine(folder, FailureCapture.ErrorsFileName)));
        string timings = await File.ReadAllTextAsync(Path.Combine(folder, "timings.md"), TestContext.Current.CancellationToken);
        Assert.Contains("(running)", timings, StringComparison.Ordinal);
        Assert.Contains("| wait for worker |", timings, StringComparison.Ordinal);
        Assert.Equal("running", (await File.ReadAllTextAsync(Path.Combine(folder, "process.txt"), TestContext.Current.CancellationToken)).Trim());
        Assert.True(new FileInfo(Path.Combine(folder, "display.png")).Length > 0);
        byte[] png = await File.ReadAllBytesAsync(Path.Combine(folder, "display.png"), TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 0x89, (byte)'P', (byte)'N', (byte)'G' }, png[..4]);

        using var tree = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(folder, "ui-tree.json"), TestContext.Current.CancellationToken));
        Assert.True(tree.RootElement.TryGetProperty("focused", out _));
        var windows = tree.RootElement.GetProperty("windows");
        Assert.NotEqual(0, windows.GetArrayLength());
        var element = windows[0].GetProperty("elements")[0];
        foreach (string property in new[] { "automationId", "name", "type", "bounds", "isVisible", "isEnabled", "hasFocus" })
        {
            Assert.True(element.TryGetProperty(property, out _), $"element lacks {property}");
        }
    }
}
