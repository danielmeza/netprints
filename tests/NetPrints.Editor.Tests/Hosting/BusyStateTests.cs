using System.Collections.ObjectModel;
using System.Reflection;
using Microsoft.Extensions.Time.Testing;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Projects;
using NetPrints.Reflection;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>The shell shows a busy state while a project loads, the references reload and the overloads of the open graphs warm up.</summary>
public sealed class BusyStateTests : IDisposable
{
    private readonly TestEditor testEditor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly FakeTimeProvider time = new();
    private readonly List<string> cleanup = [];
    private readonly List<ProjectRig> rigs = [];

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        rigs.ForEach(rig => rig.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
    }

    private ProjectRig NewRig(EditorContext context)
    {
        var rig = new ProjectRig(context, time);
        rigs.Add(rig);
        return rig;
    }

    private static async Task WaitUntilAsync(StatusBarViewModel bar, Func<bool> condition)
    {
        var done = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Check(object? sender, EventArgs e)
        {
            if (condition())
            {
                done.TrySetResult();
            }
        }

        bar.PropertyChanged += Check;
        try
        {
            Check(bar, EventArgs.Empty);
            await done.Task.WaitAsync(TimeSpan.FromSeconds(30), Token);
        }
        finally
        {
            bar.PropertyChanged -= Check;
        }
    }

    [Fact]
    public async Task LoadingAProjectIsBusyUntilItIsOpen()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ProjectRig rig = NewRig(testEditor.Context with { Projects = new GatedLoadProjectSystem(testEditor.Context.Projects, gate.Task) });

        Task load = rig.LoadProjectAsync(path);
        time.Advance(StatusBarViewModel.BusyIndicatorDelay);

        Assert.True(rig.Shell.StatusBar.IsBusy);
        Assert.Equal("Loading project…", rig.Shell.StatusBar.BusyText);
        Assert.False(load.IsCompleted);

        gate.SetResult();
        await load;

        Assert.NotNull(rig.Session);
        Assert.False(rig.Shell.StatusBar.IsBusy);
    }

    [Fact]
    public async Task ReloadingTheReferencesIsBusyUntilTheProviderIsPublished()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var host = new GatedReloadHost(testEditor.Context.Reflection, gate.Task);
        ProjectRig rig = NewRig(testEditor.Context with { Reflection = host });
        await rig.LoadProjectAsync(path);

        time.Advance(StatusBarViewModel.BusyIndicatorDelay);
        Assert.True(rig.Shell.StatusBar.IsBusy);
        Assert.Equal("Loading references…", rig.Shell.StatusBar.BusyText);

        gate.SetResult();
        await WaitUntilAsync(rig.Shell.StatusBar, () => !rig.Shell.StatusBar.IsBusy);
    }

    [Fact]
    public async Task AReloadWarmsTheOverloadsOfTheOpenProjectsGraphs()
    {
        string path = TestPaths.CopyHelloWorldSample();
        cleanup.Add(path);
        var host = new GatedReloadHost(testEditor.Context.Reflection, Task.CompletedTask);
        ProjectRig rig = NewRig(testEditor.Context with { Reflection = host });
        await rig.LoadProjectAsync(path);

        await rig.Actions.Loader.ReloadReflectionAsync();

        Assert.True(host.Spy.OverloadQueries > 0);
    }

    private sealed class GatedLoadProjectSystem(IProjectSystem inner, Task gate) : IProjectSystem
    {
        public async Task<ProjectSnapshot> LoadAsync(string projectFilePath, CancellationToken cancellationToken)
        {
            await gate;
            return await inner.LoadAsync(projectFilePath, cancellationToken);
        }

        public Task<ProjectSnapshot> ApplyAsync(string projectFilePath, IReadOnlyList<ProjectEdit> edits, CancellationToken cancellationToken) =>
            inner.ApplyAsync(projectFilePath, edits, cancellationToken);

        public Task<string> CreateAsync(string directory, string projectName, IProjectProfile profile, string rootNamespace, CancellationToken cancellationToken) =>
            inner.CreateAsync(directory, projectName, profile, rootNamespace, cancellationToken);

        public Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken) =>
            inner.BuildAsync(projectFilePath, cancellationToken);

        public ProcessStartRequest GetRunCommand(string projectFilePath) => inner.GetRunCommand(projectFilePath);
    }

    /// <summary>Reloads the wrapped host when the gate completes; its provider is wrapped to count overload queries.</summary>
    private sealed class GatedReloadHost : IReflectionHost
    {
        private readonly IReflectionHost inner;
        private readonly Task gate;

        private SpyProvider? spy;

        public GatedReloadHost(IReflectionHost inner, Task gate)
        {
            this.inner = inner;
            this.gate = gate;
        }

        public SpyProvider Spy => spy ??= SpyProvider.Create(inner.Provider);

        public bool IsLoaded => inner.IsLoaded;

        public Task Loaded => inner.Loaded;

        public IReflectionProvider Provider => Spy.Proxy;

        public ProjectSnapshot? Snapshot => inner.Snapshot;

        public ReadOnlyObservableCollection<TypeSpecifier> NonStaticTypes => inner.NonStaticTypes;

        public IReadOnlyList<string> LastWarnings => inner.LastWarnings;

        public event EventHandler? Reloaded
        {
            add => inner.Reloaded += value;
            remove => inner.Reloaded -= value;
        }

        public async Task ReloadAsync(Project project, CancellationToken cancellationToken = default)
        {
            await gate;
            await inner.ReloadAsync(project, cancellationToken);
        }
    }

    /// <summary>Forwards every call to the real provider and counts the overload and constructor queries a graph's nodes make.</summary>
    public class SpyProvider : DispatchProxy
    {
        private IReflectionProvider? target;
        private int overloadQueries;

        public int OverloadQueries => Volatile.Read(ref overloadQueries);

        public IReflectionProvider Proxy => (IReflectionProvider)(object)this;

        public static SpyProvider Create(IReflectionProvider target)
        {
            var proxy = Create<IReflectionProvider, SpyProvider>();
            SpyProvider spy = (SpyProvider)(object)proxy;
            spy.target = target;
            return spy;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            if (targetMethod.Name is nameof(IReflectionProvider.GetPublicMethodOverloads) or nameof(IReflectionProvider.GetConstructors))
            {
                Interlocked.Increment(ref overloadQueries);
            }

            return targetMethod.Invoke(target, args);
        }
    }
}
