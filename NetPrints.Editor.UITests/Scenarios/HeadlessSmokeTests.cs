using Avalonia.Headless.XUnit;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Testing.Ui.Scenarios;

namespace NetPrints.Editor.UITests.Scenarios;

/// <summary>The shared smoke flows on the headless driver.</summary>
public sealed class HeadlessSmokeTests : SmokeScenarios, IDisposable
{
    private readonly List<IDisposable> owned = [];
    private HeadlessApp? app;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    protected override Task<SmokeContext> StartAsync(CancellationToken cancellationToken)
    {
        var sample = new SampleCopy();
        owned.Add(sample);
        app = HeadlessApp.Start();
        owned.Add(app);
        string work = Directory.CreateDirectory(Path.Combine(sample.Directory, "work")).FullName;
        return Task.FromResult(new SmokeContext(app.Actor, sample.ProjectPath, work));
    }

    protected override async Task CheckpointAsync(SmokeContext context, string name, CancellationToken cancellationToken)
    {
        string folder = Path.Combine(UiArtifacts.Directory, "flows", "headless", TestContext.Current.TestMethod?.MethodName ?? "flow");
        foreach (var window in app!.Tree.Windows.Where(w => w.IsVisible).ToList())
        {
            var image = await context.Driver.ScreenshotAsync(app.Tree.KeyOf(window), cancellationToken);
            image.Save(Path.Combine(folder, $"{name}-{app.Tree.KeyOf(window)}.png"));
        }
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public Task EditCompileAndRun() => EditCompileAndRunAsync(Token);

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public Task CreateProject() => CreateProjectAsync(Token);

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public async Task AddReferences()
    {
        await AddReferencesAsync(typeof(HeadlessSmokeTests).Assembly.Location, Token);
    }

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public Task MinimizeAndRestoreClassWindow() => MinimizeAndRestoreClassWindowAsync(Token);

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public Task PanCursor() => PanCursorAsync(Token);

    [AvaloniaFact(Timeout = TestAppBuilder.Timeout)]
    public Task DragFromLists() => DragFromListsAsync(Token);

    public void Dispose()
    {
        foreach (var item in Enumerable.Reverse(owned))
        {
            item.Dispose();
        }
    }
}
