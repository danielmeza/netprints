using Avalonia.Controls;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;
using NetPrints.Testing.Ui.Driving;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The start page on its own in a window of a given size, over a recent list with fixed dates, on the headless platform.</summary>
internal sealed class StartPageRig : IDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly string directory = Path.Combine(Path.GetTempPath(), "netprints-start-" + Guid.NewGuid().ToString("N"));

    /// <summary>The real file system for the state files, with a fixed answer for which project files exist.</summary>
    private sealed class ProjectFiles(IEditorFileSystem inner, HashSet<string> existing) : IEditorFileSystem
    {
        public bool FileExists(string path) => existing.Contains(path) || (!path.EndsWith(".csproj", StringComparison.Ordinal) && inner.FileExists(path));

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);

        public void CreateDirectory(string path) => inner.CreateDirectory(path);

        public void WriteAllBytes(string path, byte[] bytes) => inner.WriteAllBytes(path, bytes);

        public byte[] ReadAllBytes(string path) => inner.ReadAllBytes(path);

        public void Move(string source, string destination) => inner.Move(source, destination);

        public void DeleteFile(string path) => inner.DeleteFile(path);

        public void DeleteDirectory(string path) => inner.DeleteDirectory(path);

        public IEnumerable<string> EnumerateDirectories(string path) => inner.EnumerateDirectories(path);

        public IEnumerable<string> EnumerateFiles(string path) => inner.EnumerateFiles(path);

        public DateTime GetLastWriteTimeUtc(string path) => inner.GetLastWriteTimeUtc(path);
    }

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    private sealed class ImmediateDispatcher : IUiDispatcher
    {
        public void Post(Action action) => action();

        public Task InvokeAsync(Action action)
        {
            action();
            return Task.CompletedTask;
        }

        public bool CheckAccess() => true;
    }

    private sealed class SettableClock : TimeProvider
    {
        public DateTimeOffset Value { get; set; }

        public override DateTimeOffset GetUtcNow() => Value;
    }

    private StartPageRig(int width, int height, bool withRecent, IEditorStateStore? startStore)
    {
        Directory.CreateDirectory(directory);
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        registry.Freeze();
        Shell = new ShellViewModel(registry, new NoServices(), TimeProvider.System, new ImmediateDispatcher());
        Actions = new NoProjectActions();
        HashSet<string> existing = [];
        var files = new ProjectFiles(new RealEditorFileSystem(), existing);
        var store = new JsonEditorStateStore(new EditorDataPaths(Path.Combine(directory, "state")), files, NullLogger.Instance);
        var clock = new SettableClock();
        var recent = new RecentProjects(store, files, clock);
        if (withRecent)
        {
            Seed(recent, clock, existing);
        }

        var services = new StartPageServices().Add<IProjectActions>(Actions).Add(recent).Add<TimeProvider>(new FakeTimeProvider(Now));
        if (startStore is not null)
        {
            services.Add(startStore);
        }

        Page = new StartPageViewModel(Shell, services);
        Ui = HeadlessUi.Create();
        Window = Ui.Show(new Window { Width = width, Height = height, Content = new StartPageView { DataContext = Page } });
    }

    public ShellViewModel Shell { get; }

    public NoProjectActions Actions { get; }

    public StartPageViewModel Page { get; }

    public HeadlessUi Ui { get; }

    public Window Window { get; }

    public static StartPageRig Create(int width, int height, bool withRecent = false, IEditorStateStore? startStore = null) => new(width, height, withRecent, startStore);

    public UiElement Element(string automationId) => new(Ui.Driver, new AutomationQuery(automationId));

    public void Dispose()
    {
        Ui.Dispose();
        Page.Dispose();
        Shell.Dispose();
        Directory.Delete(directory, recursive: true);
    }

    private static void Seed(RecentProjects recent, SettableClock clock, HashSet<string> existing)
    {
        (string Name, TimeSpan Age, bool Exists, bool Pinned)[] rows =
        [
            ("Orbital", TimeSpan.FromDays(40), true, true),
            ("HelloWorld", TimeSpan.FromHours(2), true, false),
            ("Pathfinder", TimeSpan.FromDays(3), true, false),
            ("Missing", TimeSpan.FromDays(12), false, false),
            ("Archive", TimeSpan.FromDays(90), true, false),
        ];
        foreach ((string name, TimeSpan age, bool exists, bool pinned) in rows)
        {
            string path = $"/work/projects/{name}/{name}.csproj";
            if (exists)
            {
                existing.Add(path);
            }

            clock.Value = Now - age;
            recent.Record(path, name);
            if (pinned)
            {
                recent.Pin(path);
            }
        }
    }
}
