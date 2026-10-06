using System.ComponentModel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;
using NetPrints.Serialization;
using DocumentId = NetPrints.Editor.Shell.DocumentId;

namespace NetPrints.Editor.Tests.Shell;

public sealed class ShellViewModelTests : IAsyncDisposable
{
    private static readonly string[] BuiltInPanelIds =
    [
        "netprints.panel.projectTree", "netprints.panel.inspector", "netprints.panel.variables", "netprints.panel.errors", "netprints.panel.output", "netprints.panel.csharp",
    ];

    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly FakeTimeProvider time = new();
    private readonly List<string> cleanup = [];
    private readonly List<ProjectSessionViewModel> sessions = [];

    public async ValueTask DisposeAsync()
    {
        sessions.ForEach(session => session.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
        await editor.DisposeAsync();
    }

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private ShellViewModel CreateShell(Action<IContributionRegistry>? extra = null)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        extra?.Invoke(registry);
        registry.Freeze();
        return new ShellViewModel(registry, new NoServices(), time, new InlineDispatcher());
    }

    private async Task<ProjectSessionViewModel> OpenSessionAsync()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectLoadResult loaded = await editor.Persistence.LoadAsync(path, TestContext.Current.CancellationToken);
        var session = new ProjectSessionViewModel(loaded.Project, editor.Context);
        sessions.Add(session);
        return session;
    }

    private static TestDocumentViewModel Document(string title, string key = "method:1") => new(DocumentId.Graph("A.cs", key), title);

    private static DelegateUndoableCommand Edit() => new("edit", () => { }, () => { });

    [Fact]
    public void ThePanelsAreTheBuiltInSixByDefaultDockThenOrder()
    {
        using ShellViewModel shell = CreateShell();

        Assert.Equal(BuiltInPanelIds, shell.Panels.Select(panel => panel.Id));
        Assert.Equal(["Project", "Inspector", "Variables", "Errors", "Output", "C#"], shell.Panels.Select(panel => panel.Title));
        Assert.Equal([PanelDock.Left, PanelDock.Right, PanelDock.RightLower, PanelDock.Bottom, PanelDock.Bottom, PanelDock.Bottom], shell.Panels.Select(panel => panel.DefaultDock));
        Assert.All(shell.Panels, panel => Assert.True(panel.IsVisible));
        Assert.All(shell.Panels, panel => Assert.NotNull(panel.Content));
    }

    [Fact]
    public void APanelFromAnotherContributionIsListedWithItsContentFromItsFactory()
    {
        IServiceProvider? received = null;
        var content = new object();
        using ShellViewModel shell = CreateShell(registry => registry.AddPanel(new PanelDescriptor("acme.panel.notes", "Notes", services =>
        {
            received = services;
            return content;
        }, PanelDock.Right, 5)));

        PanelViewModel notes = Assert.IsType<PanelViewModel>(shell.FindPanel("acme.panel.notes"));
        Assert.Same(content, notes.Content);
        Assert.IsType<NoServices>(received);
        Assert.Equal(7, shell.Panels.Count);
        Assert.Equal("acme.panel.notes", shell.Panels[2].Id);
        Assert.Null(shell.FindPanel("acme.panel.missing"));
    }

    [Fact]
    public void TheTitleIsJustTheProductNameWithNoProject()
    {
        using ShellViewModel shell = CreateShell();

        Assert.Equal("NetPrints", shell.Title);
    }

    [Fact]
    public async Task TheTitleFollowsTheSessionTheActiveDocumentAndTheUnsavedFiles()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        string name = session.Project.Name;
        using ShellViewModel shell = CreateShell();
        var changed = new List<string?>();
        shell.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        shell.Session = session;
        Assert.Equal($"{name} – NetPrints", shell.Title);

        TestDocumentViewModel main = Document("Main");
        shell.AddDocument(main);
        shell.ActiveDocument = main;
        Assert.Equal($"Main – {name} – NetPrints", shell.Title);

        main.Rename("Main2");
        Assert.Equal($"Main2 – {name} – NetPrints", shell.Title);

        ClassGraph cls = session.Project.Classes.Single();
        cls.MarkDirty();
        session.UndoStackFor(cls).Do(Edit());
        Assert.Equal($"Main2 – {name}* – NetPrints", shell.Title);

        Assert.True(await session.SaveAllAsync());
        Assert.Equal($"Main2 – {name} – NetPrints", shell.Title);

        shell.Session = null;
        Assert.Equal("NetPrints", shell.Title);
        Assert.Contains(nameof(ShellViewModel.Title), changed);
    }

    [Fact]
    public void AddingADocumentTwiceKeepsTheFirstOne()
    {
        using ShellViewModel shell = CreateShell();
        TestDocumentViewModel first = Document("Main");
        TestDocumentViewModel again = Document("Main again");

        Assert.Same(first, shell.AddDocument(first));
        Assert.Same(first, shell.AddDocument(again));

        Assert.Same(first, Assert.Single(shell.Documents));
        Assert.Same(first, shell.FindDocument(first.Id));
        Assert.Null(shell.FindDocument(DocumentId.StartPage));
    }

    [Fact]
    public void RemovingTheActiveDocumentActivatesThePreviousOneThenTheFirst()
    {
        using ShellViewModel shell = CreateShell();
        TestDocumentViewModel a = Document("A", "method:1");
        TestDocumentViewModel b = Document("B", "method:2");
        TestDocumentViewModel c = Document("C", "method:3");
        shell.AddDocument(a);
        shell.AddDocument(b);
        shell.AddDocument(c);
        shell.ActiveDocument = c;

        Assert.True(shell.RemoveDocument(c.Id));
        Assert.Same(b, shell.ActiveDocument);
        Assert.True(shell.RemoveDocument(a.Id));
        Assert.Same(b, shell.ActiveDocument);
        Assert.True(shell.RemoveDocument(b.Id));
        Assert.Null(shell.ActiveDocument);
        Assert.False(shell.RemoveDocument(b.Id));
        Assert.Empty(shell.Documents);
    }

    [Fact]
    public void RemovingAnInactiveDocumentKeepsTheActiveOne()
    {
        using ShellViewModel shell = CreateShell();
        TestDocumentViewModel a = Document("A", "method:1");
        TestDocumentViewModel b = Document("B", "method:2");
        shell.AddDocument(a);
        shell.AddDocument(b);
        shell.ActiveDocument = b;

        shell.RemoveDocument(a.Id);

        Assert.Same(b, shell.ActiveDocument);
    }

    [Fact]
    public async Task TheCommandStatesPulseWhenTheSessionTheActiveDocumentOrTheSessionsOwnStateChange()
    {
        ProjectSessionViewModel first = await OpenSessionAsync();
        ProjectSessionViewModel second = await OpenSessionAsync();
        using ShellViewModel shell = CreateShell();
        int pulses = 0;
        shell.CommandStatesChanged += (_, _) => pulses++;

        shell.Session = first;
        Assert.Equal(1, pulses);

        first.UndoStackFor(first.Project.Classes.Single()).Do(Edit());
        Assert.Equal(2, pulses);

        TestDocumentViewModel main = Document("Main");
        shell.AddDocument(main);
        shell.ActiveDocument = main;
        Assert.Equal(3, pulses);

        shell.Session = second;
        Assert.Equal(4, pulses);
        first.UndoStackFor(first.Project.Classes.Single()).Do(Edit());
        Assert.Equal(4, pulses);
        second.UndoStackFor(second.Project.Classes.Single()).Do(Edit());
        Assert.Equal(5, pulses);
    }

    [Fact]
    public async Task ADisposedShellStopsFollowingItsSession()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        ShellViewModel shell = CreateShell();
        shell.Session = session;
        int pulses = 0;
        shell.CommandStatesChanged += (_, _) => pulses++;

        shell.Dispose();
        session.UndoStackFor(session.Project.Classes.Single()).Do(Edit());

        Assert.Equal(0, pulses);
    }

    [Fact]
    public async Task TheBuildStateFollowsTheSessionRunningItsProgram()
    {
        ProjectSessionViewModel session = await OpenSessionAsync();
        using ShellViewModel shell = CreateShell();
        shell.Session = session;
        Assert.Equal(BuildState.Idle, shell.StatusBar.BuildState);

        await session.RunAsync();
        Assert.Equal(BuildState.Running, shell.StatusBar.BuildState);

        editor.Processes.RaiseExited(0);
        Assert.Equal(BuildState.Idle, shell.StatusBar.BuildState);

        shell.Session = null;
        Assert.Equal(BuildState.Idle, shell.StatusBar.BuildState);
    }

    [Fact]
    public void TheStatusMessageIsTheStatusBarsAndExpiresWithTheClock()
    {
        using ShellViewModel shell = CreateShell();
        var changed = new List<string?>();
        ((INotifyPropertyChanged)shell).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        shell.ShowStatus("Saved 1 file", TimeSpan.FromSeconds(4));
        Assert.Equal("Saved 1 file", shell.StatusMessage);
        time.Advance(TimeSpan.FromSeconds(4));

        Assert.Null(shell.StatusMessage);
        Assert.Equal(2, changed.Count(name => name == nameof(ShellViewModel.StatusMessage)));
    }
}
