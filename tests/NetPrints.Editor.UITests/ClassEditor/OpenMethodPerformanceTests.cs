using System.Diagnostics;
using Avalonia.Headless.XUnit;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.ClassEditor;

/// <summary>
/// Batch D1 (owner report: opening Main's graph took "several seconds" with no feedback). Measured on
/// this machine (headless, in-process): opening the class window ~320 ms, then a double click on Main
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
        using var sample = new SampleCopy();
        await using var app = HeadlessApp.Start();
        await app.OpenStartupProjectAsync(sample.ProjectPath, Token);
        var classEditor = await app.Main.OpenClassAsync(EditorSession.ClassName, Token);

        var openMain = Stopwatch.StartNew();
        await classEditor.OpenMethodAsync("Main", Token);
        openMain.Stop();

        output.WriteLine($"Batch D1: opening Main's graph took {openMain.ElapsedMilliseconds} ms");
        Assert.True(openMain.ElapsedMilliseconds < OpenMainBoundMs,
            $"opening Main took {openMain.ElapsedMilliseconds} ms, budget is {OpenMainBoundMs} ms");
    }
}
