using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Inspectors;
using NetPrints.Editor.ProjectTree;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Serialization;

namespace NetPrints.Editor.Tests.ProjectTree;

/// <summary>A shell over the built-in contributions with its tree and inspector panels attached to a fake shell API.</summary>
public sealed class ShellPanelRig : IAsyncDisposable
{
    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly List<ProjectSessionViewModel> sessions = [];
    private readonly List<string> cleanup = [];

    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    public ShellPanelRig()
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        registry.Freeze();
        Registry = registry;
        Shell = new ShellViewModel(registry, new NoServices(), TimeProvider.System, new InlineDispatcher());
        Provider = new ShellCommandContextProvider(Shell, Api);
        Invoker = new CommandInvoker(registry, Provider, exception => Faults.Add(exception));
        Shell.AttachCommands(Invoker);
        Shell.AttachPanels(Api, Invoker, editor.Context);
    }

    public ContributionRegistry Registry { get; }

    public ShellViewModel Shell { get; }

    public FakeShell Api { get; } = new();

    public ShellCommandContextProvider Provider { get; }

    public CommandInvoker Invoker { get; }

    public List<Exception> Faults { get; } = [];

    public EditorContext Context => editor.Context;

    public ProjectTreePanelViewModel Tree => Assert.IsType<ProjectTreePanelViewModel>(Shell.FindPanel(PanelContributions.ProjectTreeId)?.Content);

    public InspectorPanelViewModel Inspector => Assert.IsType<InspectorPanelViewModel>(Shell.FindPanel(PanelContributions.InspectorId)?.Content);

    public async Task<ProjectSessionViewModel> OpenSessionAsync()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        ProjectLoadResult loaded = await editor.Persistence.LoadAsync(path, TestContext.Current.CancellationToken);
        var session = new ProjectSessionViewModel(loaded.Project, editor.Context);
        sessions.Add(session);
        Shell.Session = session;
        return session;
    }

    public ProjectTreeItemViewModel Item(TreeItemKind kind, string name) =>
        Flatten(Tree.Roots).Single(item => item.Kind == kind && item.Name == name);

    public IReadOnlyList<ProjectTreeItemViewModel> Flatten(IEnumerable<ProjectTreeItemViewModel> items) =>
        [.. items.SelectMany(item => new[] { item }.Concat(Flatten(item.Children)))];

    public CommandDescriptor Command(string name) => Registry.Commands.Single(command => command.Id == ContributionIds.CommandPrefix + name);

    public async ValueTask DisposeAsync()
    {
        Shell.Dispose();
        sessions.ForEach(session => session.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
        await editor.DisposeAsync();
    }
}
