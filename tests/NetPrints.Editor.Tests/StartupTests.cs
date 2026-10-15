using System.Runtime.CompilerServices;
using Microsoft.Extensions.DependencyInjection;
using NetPrints.Core;
using NetPrints.Editor.Graph;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.UndoRedo;

namespace NetPrints.Editor.Tests;

/// <summary>
/// <see cref="Startup"/> shares one loaded reflection host across the whole run, so whatever a test leaves
/// subscribed to it would stay in memory until the run ends: each leaked editor holds a compilation over
/// every runtime assembly, and on a 16 GB CI runner the Editor test host ran out of memory and stalled.
/// </summary>
public sealed class StartupTests
{
    [Fact]
    public async Task AGraphATestLeavesOpenIsReleasedWithTheTestsScope()
    {
        var services = new ServiceCollection();
        new Startup().ConfigureServices(services);
        await using ServiceProvider root = services.BuildServiceProvider();

        WeakReference graph = await RunATestThatLeavesAGraphOpenAsync(root, TestContext.Current.CancellationToken);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(graph.IsAlive, "the shared reflection host still holds a graph of a finished test");
    }

    private static async Task<WeakReference> RunATestThatLeavesAGraphOpenAsync(ServiceProvider root, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = root.CreateAsyncScope();
        TestEditor editor = scope.ServiceProvider.GetRequiredService<TestEditor>();
        await editor.Reflection.ReloadAsync(Project.FromSnapshot(TestSnapshots.Empty("P", "N")), cancellationToken);
        return OpenAGraph(editor);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference OpenAGraph(TestEditor editor)
    {
        var classContext = new ClassContext(new ClassGraph { Name = "C", Namespace = "N" }, editor.Context, new UndoRedoStack());
        return new WeakReference(new NodeGraphViewModel(classContext.CreateMethod(), classContext.Services));
    }
}
