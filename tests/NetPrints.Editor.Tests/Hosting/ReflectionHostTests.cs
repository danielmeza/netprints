using System.Reflection;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Graph;
using NetPrints.Reflection;
using NetPrints.Translator;

namespace NetPrints.Editor.Tests.Hosting;

public class ReflectionHostTests
{
    [Fact(Timeout = 120000)]
    public async Task ReloadPublishesTypesAndRaisesReloaded()
    {
        var host = new ReflectionHost(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), NullLogger<ReflectionHost>.Instance);
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

    /// <summary>
    /// R1-16: <see cref="ReflectionHost"/> routes through the same <see cref="ProjectTranslation.TranslateAll"/>
    /// path <c>CodeAnalysisHost</c>/<c>ProjectCheck</c> use, so a broken class is skipped and reported as
    /// an <c>NPT</c> diagnostic (not a raw exception message), and several broken classes are reported in
    /// the project's own class order — not the nondeterministic order the deleted, <c>Parallel.ForEach</c>-based
    /// <c>Project.GenerateClassSources</c> produced.
    /// </summary>
    [Fact(Timeout = 120000)]
    public async Task BrokenClassesAreSkippedAndReportedInProjectOrder()
    {
        var host = new ReflectionHost(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), NullLogger<ReflectionHost>.Instance);
        var project = Project.FromSnapshot(TestSnapshots.WithRuntimeAssemblies("P", "N"));
        project.Classes.Add(WorkingClass("N.Ok"));
        project.Classes.Add(BrokenClass("N.Broken1"));
        project.Classes.Add(BrokenClass("N.Broken2"));

        await host.ReloadAsync(project, TestContext.Current.CancellationToken);

        Assert.Contains(host.NonStaticTypes, t => t.Name == "N.Ok");
        Assert.DoesNotContain(host.NonStaticTypes, t => t.Name is "N.Broken1" or "N.Broken2");
        Assert.Equal(2, host.LastWarnings.Count);
        Assert.StartsWith("N.Broken1: ", host.LastWarnings[0]);
        Assert.StartsWith("N.Broken2: ", host.LastWarnings[1]);

        ProjectTranslationResult direct = ProjectTranslation.TranslateAll(project, TranslationEnvironment.BuiltIn);
        Assert.All(direct.Diagnostics, d => Assert.Equal(TranslationDiagnosticCodes.UnsetRequiredInput, d.Id));
    }

    /// <summary>A method whose only node is its default entry/return exec chain: translates cleanly.</summary>
    private static ClassGraph WorkingClass(string fullName)
    {
        ClassGraph cls = NewClass(fullName);
        cls.Visibility = MemberVisibility.Public;
        var method = new MethodGraph("Run") { Visibility = MemberVisibility.Public };
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, method.MainReturnNode.ReturnPin);
        cls.Methods.Add(method);
        return cls;
    }

    /// <summary>A method with an <see cref="IfElseNode"/> whose condition pin is left unconnected
    /// (NPT008, the same shape as <c>HelloWorldSampleTests.HelloWorldWithIfElseAsync(null)</c>).</summary>
    private static ClassGraph BrokenClass(string fullName)
    {
        ClassGraph cls = NewClass(fullName);
        var method = new MethodGraph("Run") { Visibility = MemberVisibility.Public };
        var ifElse = new IfElseNode(method);
        GraphUtil.ConnectExecPins(method.EntryNode.InitialExecutionPin, ifElse.ExecutionPin);
        cls.Methods.Add(method);
        return cls;
    }

    private static ClassGraph NewClass(string fullName)
    {
        int lastDot = fullName.LastIndexOf('.');
        return new ClassGraph { Namespace = fullName[..lastDot], Name = fullName[(lastDot + 1)..] };
    }

    [Fact(Timeout = 120000)]
    public async Task ProjectClassesAreVisibleToReflection()
    {
        var host = new ReflectionHost(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), NullLogger<ReflectionHost>.Instance);
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

    [Fact(Timeout = 120000)]
    public async Task ACatalogAddsItsTypesAndReplacesTheLiveTypesOfTheAssembliesItCovers()
    {
        var project = Project.FromSnapshot(TestSnapshots.WithRuntimeAssemblies("P", "N"));
        var plain = new ReflectionHost(new InlineDispatcher(), TestExtensions.CreateBuiltIn(), NullLogger<ReflectionHost>.Instance);
        await plain.ReloadAsync(project, TestContext.Current.CancellationToken);

        var manifest = new ExtensionManifest("editor.test", "Editor test", "1.0.0", string.Empty, "1.0", []);
        await using var extensions = new ExtensionHost(
            new ExtensionLoaderOptions([], [], [BuiltInExtension.InProcessEntry, (manifest, new CatalogExtension())]), NullLoggerFactory.Instance);
        var withCatalog = new ReflectionHost(new InlineDispatcher(), extensions, NullLogger<ReflectionHost>.Instance);
        await withCatalog.ReloadAsync(project, TestContext.Current.CancellationToken);

        Assert.Contains(plain.NonStaticTypes, t => t.Name == "System.Text.RegularExpressions.Regex");
        Assert.DoesNotContain(withCatalog.NonStaticTypes, t => t.Name == "System.Text.RegularExpressions.Regex");
        Assert.Contains(withCatalog.NonStaticTypes, t => t.Name == "Acme.Widget");
        Assert.Contains(withCatalog.Provider.GetNonStaticTypes(), t => t.Name == "Acme.Widget");
        Assert.Contains(withCatalog.NonStaticTypes, t => t == TypeSpecifier.FromType<string>());
    }

    [Fact(Timeout = 120000)]
    public async Task TheTestExtensionsCatalogTypeReachesTheReflectionHost() // SC-004
    {
        var project = Project.FromSnapshot(TestSnapshots.WithRuntimeAssemblies("P", "N"));
        await using ExtensionHost extensions = TestExtensionFolder.CreateHost();
        var host = new ReflectionHost(new InlineDispatcher(), extensions, NullLogger<ReflectionHost>.Instance);

        await host.ReloadAsync(project, TestContext.Current.CancellationToken);

        var widget = new TypeSpecifier("NetPrints.TestLib.Widget");
        Assert.Contains(host.NonStaticTypes, t => t == widget);
        Assert.Contains(host.Provider.GetNonStaticTypes(), t => t == widget);
        Assert.Contains(host.NonStaticTypes, t => t == TypeSpecifier.FromType<string>());
    }

    [Fact(Timeout = 120000)]
    public async Task AReloadCancelledDuringItsWarmUpStopsThereAndPublishesNothing()
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        CancellingCatalog catalog = CancellingCatalog.Create(cancellation);
        var manifest = new ExtensionManifest("editor.test", "Editor test", "1.0.0", string.Empty, "1.0", []);
        await using var extensions = new ExtensionHost(
            new ExtensionLoaderOptions([], [], [BuiltInExtension.InProcessEntry, (manifest, new GivenCatalogExtension(catalog.Catalog))]), NullLoggerFactory.Instance);
        var host = new ReflectionHost(new InlineDispatcher(), extensions, NullLogger<ReflectionHost>.Instance);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => host.ReloadAsync(Project.FromSnapshot(TestSnapshots.Empty("P", "N")), cancellation.Token));

        Assert.Equal(1, catalog.MethodsTaken);
        Assert.False(host.IsLoaded);
    }

    /// <summary>An empty type catalog whose method enumeration cancels the reload, then offers three static methods.</summary>
    public class CancellingCatalog : DispatchProxy
    {
        private ITypeCatalog? target;
        private CancellationTokenSource? cancellation;
        private int methodsTaken;

        public int MethodsTaken => Volatile.Read(ref methodsTaken);

        public ITypeCatalog Catalog => (ITypeCatalog)(object)this;

        public static CancellingCatalog Create(CancellationTokenSource cancellation)
        {
            var catalog = (CancellingCatalog)(object)Create<ITypeCatalog, CancellingCatalog>();
            catalog.target = new InMemoryTypeCatalog(new CatalogInfo("editor.test/cancelling", "1.0.0", []), [], [], [], [],
                new Dictionary<TypeSpecifier, IReadOnlyList<string>>(), new Dictionary<MethodSpecifier, string>());
            catalog.cancellation = cancellation;
            return catalog;
        }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            ArgumentNullException.ThrowIfNull(targetMethod);
            return targetMethod.Name == nameof(IReflectionProvider.GetMethods) ? CancelThenOfferMethods() : targetMethod.Invoke(target, args);
        }

        private IEnumerable<MethodSpecifier> CancelThenOfferMethods()
        {
            cancellation?.Cancel();
            for (int i = 0; i < 3; i++)
            {
                Interlocked.Increment(ref methodsTaken);
                yield return new MethodSpecifier($"M{i}", [], [], MethodModifiers.Static, MemberVisibility.Public, new TypeSpecifier("Acme.Tools"), []);
            }
        }
    }

    private sealed class GivenCatalogExtension(ITypeCatalog catalog) : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder) => builder.AddTypeCatalog(catalog);
    }

    private sealed class CatalogExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder) => builder.AddTypeCatalog(new InMemoryTypeCatalog(
            new CatalogInfo("editor.test/catalog", "1.0.0", ["System.Text.RegularExpressions"]),
            [new TypeSpecifier("Acme.Widget")], [], [], [],
            new Dictionary<TypeSpecifier, IReadOnlyList<string>>(), new Dictionary<MethodSpecifier, string>()));
    }
}
