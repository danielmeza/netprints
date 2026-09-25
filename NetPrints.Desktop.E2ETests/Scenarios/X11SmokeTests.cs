using NetPrints.Desktop.E2ETests.Driving;
using NetPrints.Desktop.E2ETests.Hosting;
using NetPrints.Testing.Ui.Scenarios;
using NetPrints.Testing.Ui.Screenplay;

namespace NetPrints.Desktop.E2ETests.Scenarios;

/// <summary>The shared smoke flows on the real desktop editor (X11, xdotool, GTK pickers).</summary>
[Collection(DesktopCollection.Name)]
public sealed class X11SmokeTests(XServer server) : SmokeScenarios, IAsyncDisposable
{
    private const int Timeout = 180_000;

    private readonly string work = Directory.CreateTempSubdirectory("netprints-e2e-").FullName;
    private EditorProcess? editor;
    private X11Driver? driver;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static string Artifacts => Path.Combine(
        Environment.GetEnvironmentVariable("NETPRINTS_UI_ARTIFACTS") is { Length: > 0 } configured ? configured : Path.Combine(AppContext.BaseDirectory, "ui-artifacts"),
        "e2e", TestContext.Current.TestMethod?.MethodName ?? "test");

    protected override async Task<SmokeContext> StartAsync(CancellationToken cancellationToken)
    {
        if (!XServer.IsEnabled)
        {
            Assert.Skip($"Desktop E2E tests run with {XServer.EnableVariable}=1 (Linux with Xvfb, openbox, xdotool, ImageMagick and GTK 3).");
        }

        // Arrange: a private copy of the sample; the editor starts without a project.
        string sample = Directory.CreateDirectory(Path.Combine(work, "HelloWorld")).FullName;
        foreach (string file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "samples", "HelloWorld")))
        {
            File.Copy(file, Path.Combine(sample, Path.GetFileName(file)));
        }

        editor = await EditorProcess.StartAsync(server, work, project: null, cancellationToken);
        driver = new X11Driver(server, editor, new Tool(server));
        var actor = Actor.Named("Ada").WhoCan(UseNetPrints.With(driver, new GtkFileDialogs(driver, editor)));
        await actor.Using<UseNetPrints>().MainWindow.GetAsync(cancellationToken);
        await CheckpointAsync(new SmokeContext(actor, "", work), "00-started", cancellationToken);
        return new SmokeContext(actor, Path.Combine(sample, "HelloWorld.netpp"), Directory.CreateDirectory(Path.Combine(work, "out")).FullName);
    }

    protected override async Task CheckpointAsync(SmokeContext context, string name, CancellationToken cancellationToken)
    {
        var screen = await driver!.ScreenAsync(cancellationToken);
        screen.Save(Path.Combine(Artifacts, name + ".png"));
    }

    [Fact(Timeout = Timeout)]
    public Task EditCompileAndRun() => EditCompileAndRunAsync(Token);

    [Fact(Timeout = Timeout)]
    public Task CreateProject() => CreateProjectAsync(Token);

    [Fact(Timeout = Timeout)]
    public Task AddReferences() => AddReferencesAsync(typeof(object).Assembly.Location, Token);

    [Fact(Timeout = Timeout)]
    public Task MinimizeAndRestoreClassWindow() => MinimizeAndRestoreClassWindowAsync(Token);

    [Fact(Timeout = Timeout)]
    public Task PanCursor() => PanCursorAsync(Token);

    [Fact(Timeout = Timeout)]
    public Task DragFromLists() => DragFromListsAsync(Token);

    /// <summary>Diagnostics for every test (the last state of a failing one): screen, UI dump, logs, xdotool calls.</summary>
    public async ValueTask DisposeAsync()
    {
        if (editor is not null && driver is not null)
        {
            try
            {
                Directory.CreateDirectory(Artifacts);
                (await driver.ScreenAsync(CancellationToken.None)).Save(Path.Combine(Artifacts, "zz-final.png"));
                await File.WriteAllTextAsync(Path.Combine(Artifacts, "tree.txt"), await driver.DumpAsync(CancellationToken.None));
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or TimeoutException)
            {
                // Best effort: the editor may have crashed (see its log).
            }

            await File.WriteAllTextAsync(Path.Combine(Artifacts, "editor-stdout.txt"), editor.Output);
            await File.WriteAllTextAsync(Path.Combine(Artifacts, "editor-stderr.txt"), editor.Errors);
            await File.WriteAllTextAsync(Path.Combine(Artifacts, "xdotool.txt"), driver.Tool.Log);
            await editor.DisposeAsync();
        }

        try
        {
            Directory.Delete(work, true);
        }
        catch (IOException)
        {
            // Best effort.
        }
    }
}
