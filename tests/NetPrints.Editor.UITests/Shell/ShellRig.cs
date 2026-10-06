using Avalonia.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Hosting.Automation;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Shell.Docking;
using NetPrints.Editor.UITests.Driving;
using NetPrints.Editor.UITests.Hosting;

namespace NetPrints.Editor.UITests.Shell;

/// <summary>A document with no editor behind it: its content is its own text.</summary>
internal sealed class TestDocumentViewModel(DocumentId id, string title) : DocumentViewModel(id, title)
{
    public override string ToString() => Title;
}

/// <summary>Project flows that a shell layout test never reaches.</summary>
internal sealed class NoProjectActions : IProjectActions
{
    public Task<bool> ConfirmUnloadAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task OpenProjectAsync(string? path, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task NewProjectAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task CloseProjectAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task ExitAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public void ShowProjectSettings() => throw new NotSupportedException();

    public Task ShowKeyboardShortcutsAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task ShowAboutAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task NewClassAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task AddExistingClassAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task ShowReferencesAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public void ShowClassSettings(ClassGraph cls) => throw new NotSupportedException();

    public void AddMethod(ClassGraph cls) => throw new NotSupportedException();

    public void AddConstructor(ClassGraph cls) => throw new NotSupportedException();

    public void AddVariable(ClassGraph cls) => throw new NotSupportedException();

    public void AddEventGraph(ClassGraph cls) => throw new NotSupportedException();

    public Task OverrideMethodAsync(ClassGraph cls, CancellationToken cancellationToken) => throw new NotSupportedException();

    public void RenameItem(object item) => throw new NotSupportedException();

    public Task DeleteItemAsync(object item, CancellationToken cancellationToken) => throw new NotSupportedException();
}

/// <summary>The shell state, the docking adapter and a window hosting its layout, on the headless platform.</summary>
internal sealed class ShellRig : IDisposable
{
    public const int Width = 1600;
    public const int Height = 1000;

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

    private ShellRig(ShellViewModel shell, DockShellAdapter adapter, HeadlessUi ui, Window main)
    {
        Shell = shell;
        Adapter = adapter;
        Ui = ui;
        Main = main;
    }

    public ShellViewModel Shell { get; }

    public DockShellAdapter Adapter { get; }

    public IShell Api => Adapter;

    public HeadlessUi Ui { get; }

    public Window Main { get; }

    public static ShellRig Create(Func<DocumentId, DocumentViewModel?>? documents = null, ILogger? logger = null)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        registry.Freeze();
        var shell = new ShellViewModel(registry, new NoServices(), TimeProvider.System, new ImmediateDispatcher());
        var adapter = new DockShellAdapter(shell, new NoProjectActions(), documents ?? (id => new TestDocumentViewModel(id, id.GraphKey ?? id.ToString())), logger);
        shell.Layout = adapter;
        var ui = HeadlessUi.Create();
        Window main = ui.Show(new Window { Width = Width, Height = Height, Content = new DockHost { DataContext = adapter } });
        return new ShellRig(shell, adapter, ui, main);
    }

    public static AutomationQuery Panel(string panelId) => new(AutomationIds.ShellPanelPrefix + panelId);

    public static AutomationQuery Content(DocumentId id) => new(AutomationIds.ShellDocumentPrefix + id);

    public string MainKey => Ui.Tree.KeyOf(Main);

    public void Settle() => HeadlessDriver.Pump();

    public AutomationElement? FindOne(AutomationQuery query) => Ui.Tree.Find(query).SingleOrDefault();

    public void Dispose()
    {
        Ui.Dispose();
        Shell.Dispose();
    }
}
