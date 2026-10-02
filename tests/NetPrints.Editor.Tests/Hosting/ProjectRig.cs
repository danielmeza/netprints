using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.ClassEditor;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Contributions.BuiltIn;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>A shell state with the project flows of the shell window over it, as the shell host composes them, without a window.</summary>
internal sealed class ProjectRig : IDisposable
{
    private sealed class NoServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }

    public ProjectRig(EditorContext context, Func<ClassGraph, ClassEditorViewModel>? editorFor = null)
    {
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        BuiltInContributions.Register(registry);
        registry.Freeze();
        Shell = new ShellViewModel(registry, new NoServices(), TimeProvider.System, new InlineDispatcher());
        Actions = new ShellProjectActions(context, Shell, editorFor ?? (_ => throw new InvalidOperationException("No class editor.")));
    }

    public ShellViewModel Shell { get; }

    public ShellProjectActions Actions { get; }

    public ProjectSessionViewModel? Session => Shell.Session;

    public Project? Project => Session?.Project;

    public Task LoadProjectAsync(string path) => Actions.Loader.LoadProjectAsync(path);

    public Task ReportExtensionFailuresAsync() => Actions.Loader.ReportExtensionFailuresAsync();

    public Task NewClassAsync() => Actions.NewClassAsync(TestContext.Current.CancellationToken);

    public void Dispose()
    {
        Shell.Dispose();
        Actions.Dispose();
    }
}
