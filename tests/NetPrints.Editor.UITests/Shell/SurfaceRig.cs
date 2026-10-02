using Avalonia.Controls;
using Avalonia.LogicalTree;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>The shell window over the real registry, the real command context provider and, optionally, an open project session.</summary>
internal sealed class SurfaceRig : IDisposable
{
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

    private SurfaceRig(ContributionRegistry registry, ShellViewModel shell, DockShellAdapter adapter, CommandInvoker invoker, HeadlessUi ui, ShellWindow window, List<Exception> faults)
    {
        Faults = faults;
        Registry = registry;
        Shell = shell;
        Adapter = adapter;
        Invoker = invoker;
        Ui = ui;
        Window = window;
    }

    public ContributionRegistry Registry { get; }

    public ShellViewModel Shell { get; }

    public DockShellAdapter Adapter { get; }

    public CommandInvoker Invoker { get; }

    public HeadlessUi Ui { get; }

    public ShellWindow Window { get; }

    public List<Exception> Faults { get; }

    public static SurfaceRig Create(Action<IContributionRegistry>? extra = null, ProjectSessionViewModel? session = null)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        extra?.Invoke(registry);
        registry.Freeze();
        var shell = new ShellViewModel(registry, new NoServices(), TimeProvider.System, new ImmediateDispatcher());
        var adapter = new DockShellAdapter(shell, new NoProjectActions(), id => new TestDocumentViewModel(id, id.GraphKey ?? id.ToString()));
        shell.Layout = adapter;
        var faults = new List<Exception>();
        var invoker = new CommandInvoker(registry, new ShellCommandContextProvider(shell, adapter), faults.Add);
        shell.AttachCommands(invoker);
        shell.Session = session;
        var ui = HeadlessUi.Create();
        ShellWindow window = ui.Show(new ShellWindow { DataContext = shell, Width = ShellRig.Width, Height = ShellRig.Height });
        return new SurfaceRig(registry, shell, adapter, invoker, ui, window, faults);
    }

    public static AutomationQuery Id(string automationId) => new(automationId);

    public Control? Find(string automationId) => Ui.Tree.FindControls(Id(automationId)).Select(pair => pair.Control).FirstOrDefault();

    public IReadOnlyList<Control> FindAll(string automationId) => [.. Ui.Tree.FindControls(Id(automationId)).Select(pair => pair.Control)];

    public IReadOnlyList<MenuItem> TopMenus() =>
        Assert.IsType<Menu>(Find(AutomationIds.ShellMenuBar)).GetLogicalChildren().OfType<MenuItem>().ToList();

    public IReadOnlyList<MenuItem> Open(string header)
    {
        MenuItem menu = Assert.IsType<MenuItem>(Find(AutomationIds.MenuPrefix + header));
        menu.IsSubMenuOpen = true;
        HeadlessDriver.Pump();
        return [.. menu.GetLogicalChildren().OfType<MenuItem>()];
    }

    public void Settle() => HeadlessDriver.Pump();

    public void Dispose()
    {
        Ui.Dispose();
        Shell.Dispose();
    }
}
