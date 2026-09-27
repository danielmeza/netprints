using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Hosting;

namespace NetPrints.Editor.Tests.Hosting;

public class ReflectionHostTests
{
    [Fact(Timeout = 120000)]
    public async Task ReloadPublishesTypesAndRaisesReloaded()
    {
        var host = new ReflectionHost(new InlineDispatcher(), NullLogger<ReflectionHost>.Instance);
        int reloaded = 0;
        host.Reloaded += (_, _) => reloaded++;

        Assert.Empty(host.NonStaticTypes);
        Assert.False(host.IsLoaded);
        Assert.False(host.Loaded.IsCompleted);
        Assert.Null(host.Snapshot);
        Assert.Throws<InvalidOperationException>(() => host.Provider); // no silent empty provider

        var project = Project.FromSnapshot(TestSnapshots.WithRuntimeAssemblies("P", "N"));
        await host.ReloadAsync(project, TestContext.Current.CancellationToken);

        Assert.Equal(1, reloaded);
        Assert.True(host.IsLoaded);
        Assert.True(host.Loaded.IsCompletedSuccessfully);
        Assert.Same(project.Snapshot, host.Snapshot);
        Assert.True(host.NonStaticTypes.Count > 4000);
        Assert.Empty(host.LastWarnings);
        Assert.True(host.Provider.GetNonStaticTypes().Contains(TypeSpecifier.FromType<string>()));
    }

    [Fact]
    public async Task ReloadRequiresASnapshot()
    {
        var host = new ReflectionHost(new InlineDispatcher(), NullLogger<ReflectionHost>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => host.ReloadAsync(Project.CreateNew("P", "N"), TestContext.Current.CancellationToken));
    }

    [Fact(Timeout = 120000)]
    public async Task ProjectClassesAreVisibleToReflection()
    {
        var host = new ReflectionHost(new InlineDispatcher(), NullLogger<ReflectionHost>.Instance);
        var project = await TestPaths.LoadHelloWorldCopyAsync(TestContext.Current.CancellationToken);
        try
        {
            await host.ReloadAsync(project, TestContext.Current.CancellationToken);
            Assert.True(host.NonStaticTypes.Any(t => t.Name == "HelloWorld.Program"));
        }
        finally
        {
            TestPaths.TryDelete(project.Path);
        }
    }
}
