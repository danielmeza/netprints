using System.Diagnostics;
using Avalonia.Headless.XUnit;
using NetPrints.Testing.Ui.Shell;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>
/// Batch D1 (owner report: opening Main's graph took "several seconds" with no feedback). Measured on
/// this machine (headless, in-process): opening the shell, then a double click on Main
/// ~130-220 ms (mostly Avalonia's first layout pass and control-template realization, not application
/// code); a real windowed run adds real rendering and X11 round trips on top. The bound below is
/// generous (a few times the measured cost, R2-19) so a busy CI runner does not fail the build, while
/// still catching a real regression back toward "several seconds": a 5000 ms bound (25x measured) would
/// let a 3-4 second regression through unnoticed.
/// </summary>
public class OpenMethodPerformanceTests
{
    private const double OpenMainBoundMs = 1500;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task OpeningMainStaysWithinBudget()
    {
        var output = TestContext.Current.TestOutputHelper
            ?? throw new InvalidOperationException($"{nameof(TestContext)} has no {nameof(TestContext.Current.TestOutputHelper)}.");
        await using var app = ShellApp.Start();
        await app.OpenSampleAsync(Token);
        var shell = await new ShellPage(app.Driver).WaitShownAsync(Token);

        var openMain = Stopwatch.StartNew();
        await shell.OpenMethodAsync("Main", Token);
        openMain.Stop();

        output.WriteLine($"Batch D1: opening Main's graph took {openMain.ElapsedMilliseconds} ms");
        Assert.True(openMain.ElapsedMilliseconds < OpenMainBoundMs,
            $"opening Main took {openMain.ElapsedMilliseconds} ms, budget is {OpenMainBoundMs} ms");
    }
}
