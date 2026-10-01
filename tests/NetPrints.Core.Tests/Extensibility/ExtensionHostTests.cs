using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility;

/// <summary><see cref="ExtensionHost"/>: registry rebuilds and the load-context cache.</summary>
public class ExtensionHostTests : IDisposable
{
    private readonly string root = NewTempDirectory();

    public void Dispose() => Directory.Delete(root, recursive: true);

    private string CompiledExtension(string id)
    {
        string folder = Path.Combine(root, id);
        WriteManifest(folder, ManifestJson(id, id + ".dll"));
        Compile(folder, id, """
            using NetPrints.Extensibility;
            public class Ext : INetPrintsExtension
            {
                public void Register(IExtensionBuilder builder) => builder.AddClassEmitter(new Emitter());
            }
            public class Emitter : NetPrints.Translator.IClassEmitter
            {
                public string Id => "compiled";
                public void EmitClass(NetPrints.Translator.ClassEmitContext context) { }
            }
            """, ExperimentalApiIds.Emitters);
        return folder;
    }

    [Fact]
    public async Task CurrentIsLoadedFromTheOptions()
    {
        var disposable = new DisposableMemberEmitter();
        await using var host = new ExtensionHost(
            Options([BuiltInExtension.InProcessEntry, InProcess("test.ext", builder => builder.AddMemberEmitter(disposable))]),
            new CollectingLoggerFactory());

        Assert.Equal(["netprints", "test.ext"], host.Current.Loaded.Select(m => m.Id));
    }

    [Fact]
    public async Task LoadingForAProjectRebuildsTheRegistryOnceAndReusesTheLoadContext()
    {
        string folder = CompiledExtension("test.compiled");
        var emitters = new List<DisposableMemberEmitter>();
        await using var host = new ExtensionHost(
            Options([BuiltInExtension.InProcessEntry, InProcess("test.ext", builder =>
            {
                var emitter = new DisposableMemberEmitter();
                emitters.Add(emitter);
                builder.AddMemberEmitter(emitter);
            })]),
            new CollectingLoggerFactory());
        ExtensionRegistry initial = host.Current;
        var raised = new List<ExtensionRegistry>();
        host.RegistryChanged += (_, registry) => raised.Add(registry);

        ExtensionRegistry withProject = await host.LoadForProjectAsync([folder], TestContext.Current.CancellationToken);
        ExtensionRegistry again = await host.LoadForProjectAsync([folder], TestContext.Current.CancellationToken);

        Assert.NotSame(initial, withProject);
        Assert.Same(withProject, again);
        Assert.Same(withProject, host.Current);
        Assert.Same(withProject, Assert.Single(raised));
        Assert.Contains(withProject.Loaded, m => m.Id == "test.compiled");
        Assert.DoesNotContain(initial.Loaded, m => m.Id == "test.compiled");
        Assert.True(emitters[0].Disposed);
        Assert.False(emitters[1].Disposed);

        ExtensionRegistry dropped = await host.LoadForProjectAsync([], TestContext.Current.CancellationToken);
        ExtensionRegistry restored = await host.LoadForProjectAsync([folder], TestContext.Current.CancellationToken);

        Assert.DoesNotContain(dropped.Loaded, m => m.Id == "test.compiled");
        Assert.Same(
            withProject.ClassEmitters.Single().GetType().Assembly,
            restored.ClassEmitters.Single().GetType().Assembly);
    }

    // R1-11: two overlapping LoadForProjectAsync calls for the same folders must be serialized, so the
    // second one re-checks projectFolders after acquiring the gate and returns the first one's registry
    // instead of loading (and racing to swap `current`) again.
    [Fact]
    public async Task ConcurrentLoadForProjectCallsAreSerializedAndBothReturnTheSameRegistry()
    {
        string folder = Path.Combine(root, "test.slow");
        WriteManifest(folder, ManifestJson("test.slow", "test.slow.dll"));
        Compile(folder, "test.slow", """
            using NetPrints.Extensibility;
            public class Ext : INetPrintsExtension
            {
                public void Register(IExtensionBuilder builder) => System.Threading.Thread.Sleep(300);
            }
            """);

        await using var host = new ExtensionHost(ExtensionLoaderOptions.BuiltInOnly, new CollectingLoggerFactory());
        var raised = new List<ExtensionRegistry>();
        host.RegistryChanged += (_, registry) => raised.Add(registry);

        Task<ExtensionRegistry> first = Task.Run(async () => await host.LoadForProjectAsync([folder], TestContext.Current.CancellationToken));
        await Task.Delay(50, TestContext.Current.CancellationToken);
        Task<ExtensionRegistry> second = Task.Run(async () => await host.LoadForProjectAsync([folder], TestContext.Current.CancellationToken));

        ExtensionRegistry[] results = await Task.WhenAll(first, second);

        Assert.Same(results[0], results[1]);
        Assert.Same(results[0], host.Current);
        Assert.Same(results[0], Assert.Single(raised));
    }

    [Fact]
    public async Task ACancelledLoadLeavesTheRegistryUnchanged()
    {
        string folder = CompiledExtension("test.compiled");
        await using var host = new ExtensionHost(ExtensionLoaderOptions.BuiltInOnly, new CollectingLoggerFactory());
        ExtensionRegistry initial = host.Current;
        using var cancelled = new System.Threading.CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => host.LoadForProjectAsync([folder], cancelled.Token).AsTask());

        Assert.Same(initial, host.Current);
    }
}
