using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;
using NetPrints.Graph;
using NetPrints.Testing.Extensions;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility.MultiExtension;

/// <summary>
/// MX-T01: what the loader does before the ADR-0010 §4 change (host type identity, NPX001-NPX007, ordering), pinned with the
/// fixture extensions. Written against the existing loader, so green from the first run; the tests marked "current behaviour"
/// pin a result the ADR-0010 rules replace and are updated by the batch that changes it.
/// </summary>
[Collection(nameof(RealExtensionLoadCollection))]
public sealed class CharacterizationTests : IAsyncLifetime
{
    private readonly string root = Directory.CreateTempSubdirectory("netprints-characterization-").FullName;

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        Directory.Delete(root, recursive: true);
        return ValueTask.CompletedTask;
    }

    private string Copy(string id, string? folderName = null) => FixtureExtensions.CopyTo(root, id, folderName);

    private static Task<ExtensionHarness> LoadAsync(string[] folders, params INetPrintsExtension[] inProcess) =>
        ExtensionHarness.CreateAsync(folders, inProcess, TestContext.Current.CancellationToken);

    private static ExtensionLoadResult.Failed Failure(ExtensionRegistry registry, string id) => SingleFailure(registry, id);

    private static string[] LoadedIds(ExtensionRegistry registry) => [.. registry.Loaded.Select(m => m.Id)];

    private static void AssertNeighboursIntact(ExtensionRegistry registry)
    {
        Assert.Contains(FixtureExtensions.Alpha, LoadedIds(registry));
        Assert.Contains(FixtureExtensions.Beta, LoadedIds(registry));
        Assert.Contains(registry.NodeKinds, k => k.Kind == "fx.alpha/Ping");
        Assert.Contains(registry.NodeKinds, k => k.Kind == "fx.beta/Pong");
    }

    private Task<ExtensionHarness> LoadWithNeighboursAsync(params string[] more) =>
        LoadAsync([Copy(FixtureExtensions.Alpha), Copy(FixtureExtensions.Beta), .. more]);

    [Fact]
    public async Task ExtensionTypesAreTheHostsTypesAndTheExtensionAssemblyLivesInItsOwnContext()
    {
        await using ExtensionHarness harness = await LoadWithNeighboursAsync();

        var ping = Assert.Single(harness.Registry.NodeKinds, k => k.Kind == "fx.alpha/Ping");
        Assert.Same(typeof(Node), ping.NodeType.BaseType);
        Assert.True(typeof(Node).IsAssignableFrom(ping.NodeType));
        Assert.Same(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(typeof(Node).Assembly));
        AssemblyLoadContext? extensionContext = AssemblyLoadContext.GetLoadContext(ping.NodeType.Assembly);
        Assert.NotNull(extensionContext);
        Assert.NotSame(AssemblyLoadContext.Default, extensionContext);
        Assert.Equal(FixtureExtensions.Alpha, extensionContext.Name);

        Assembly[] coreCopies = [.. AssemblyLoadContext.All.SelectMany(c => c.Assemblies).Where(a => a.GetName().Name == "NetPrints.Core")];
        Assert.Single(coreCopies);
    }

    [Fact]
    public async Task ACopyOfAHostAssemblyInTheExtensionFolderIsIgnoredSilently()
    {
        string folder = Copy(FixtureExtensions.Alpha);
        File.Copy(typeof(Node).Assembly.Location, Path.Combine(folder, "NetPrints.Core.dll"));
        var logs = new CollectingLoggerFactory();

        await using ExtensionRegistry registry = Load(Options(folders: [folder]), logs);

        Assert.Equal([FixtureExtensions.Alpha], LoadedIds(registry));
        var ping = Assert.Single(registry.NodeKinds);
        Assert.Same(typeof(Node), ping.NodeType.BaseType);
        Assert.DoesNotContain(logs.Entries, e => e.Level >= Microsoft.Extensions.Logging.LogLevel.Warning);
    }

    [Fact]
    public async Task ExtensionsLoadInDependencyOrderWithOrdinalIdTiesWhateverTheDiscoveryOrder()
    {
        string alpha = Copy(FixtureExtensions.Alpha);
        string beta = Copy(FixtureExtensions.Beta);
        string lib = Copy(FixtureExtensions.LibV1);

        foreach (string[] folders in new[] { new[] { alpha, beta, lib }, [beta, alpha, lib], [lib, beta, alpha], [lib, alpha, beta] })
        {
            await using ExtensionHarness harness = await LoadAsync(folders);

            string[] expected = [BuiltInExtension.InProcessEntry.Manifest.Id, FixtureExtensions.Alpha, FixtureExtensions.Beta, FixtureExtensions.LibV1];
            Assert.Equal(expected, LoadedIds(harness.Registry));
            string[] kinds = [.. harness.Registry.NodeKinds.Select(k => k.Kind).Where(k => k.StartsWith("fx.", StringComparison.Ordinal))];
            Assert.Equal(["fx.alpha/Ping", "fx.beta/Pong", "fx.libv1/Describe"], kinds);
        }
    }

    [Fact]
    public async Task Npx001AnUnreadableManifestFailsOnlyThatFolder()
    {
        string broken = Path.Combine(root, "broken");
        WriteManifest(broken, "{ not json");

        await using ExtensionHarness harness = await LoadWithNeighboursAsync(broken);

        Assert.Equal("NPX001", Failure(harness.Registry, "broken").Code);
        AssertNeighboursIntact(harness.Registry);
    }

    [Fact]
    public async Task Npx002AnIncompatibleApiVersionFailsOnlyThatExtension()
    {
        string future = Path.Combine(root, "future");
        WriteManifest(future, ManifestJson("fx.future", "fx.future.dll", api: "9.0"));

        await using ExtensionHarness harness = await LoadWithNeighboursAsync(future);

        Assert.Equal("NPX002", Failure(harness.Registry, "fx.future").Code);
        AssertNeighboursIntact(harness.Registry);
    }

    [Fact]
    public async Task Npx003AMissingDependencyFailsOnlyTheDependent()
    {
        string dependent = Path.Combine(root, "dependent");
        WriteManifest(dependent, ManifestJson("fx.dependent", "fx.dependent.dll", "1.0", "fx.absent"));

        await using ExtensionHarness harness = await LoadWithNeighboursAsync(dependent);

        Assert.Equal("NPX003", Failure(harness.Registry, "fx.dependent").Code);
        AssertNeighboursIntact(harness.Registry);
    }

    [Fact]
    public async Task Npx004ADuplicateIdFailsTheLaterFolderAndTheFirstWins()
    {
        string duplicate = Copy(FixtureExtensions.Alpha, "fx.alpha-second");

        await using ExtensionHarness harness = await LoadWithNeighboursAsync(duplicate);

        ExtensionLoadResult.Failed failed = Failure(harness.Registry, FixtureExtensions.Alpha);
        Assert.Equal("NPX004", failed.Code);
        Assert.Equal(Path.Combine(duplicate, ExtensionManifest.FileName), failed.ManifestPath);
        Assert.Single(harness.Registry.NodeKinds, k => k.Kind == "fx.alpha/Ping");
        AssertNeighboursIntact(harness.Registry);
    }

    [Fact]
    public async Task Npx005ARegisterThatThrowsFailsOnlyThatExtension()
    {
        var thrower = new DelegateExtension(_ => throw new InvalidOperationException("boom"));

        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync(
            [Copy(FixtureExtensions.Alpha), Copy(FixtureExtensions.Beta)], [thrower], TestContext.Current.CancellationToken);

        ExtensionLoadResult.Failed failed = Failure(harness.Registry, "harness.inprocess.0");
        Assert.Equal("NPX005", failed.Code);
        Assert.Contains("boom", failed.Reason, StringComparison.Ordinal);
        AssertNeighboursIntact(harness.Registry);
    }

    [Fact]
    public async Task Npx006AContributionThatClaimsATakenIdIsRejectedAndTheExtensionStillLoads()
    {
        var squatter = InProcess("fx.squatter", builder => builder.AddProjectProfile(new StubProfile("fx.alpha.profile")), "1.0", FixtureExtensions.Alpha);

        await using ExtensionRegistry registry = Load(Options(inProcess: [squatter], folders: [Copy(FixtureExtensions.Alpha), Copy(FixtureExtensions.Beta)]));

        ExtensionContributionIssue issue = Assert.Single(registry.Issues);
        Assert.Equal("NPX006", issue.Code);
        Assert.Equal("fx.squatter", issue.ExtensionId);
        Assert.Contains("fx.squatter", LoadedIds(registry));
        Assert.Single(registry.Profiles, p => p.Id == "fx.alpha.profile");
        AssertNeighboursIntact(registry);
    }

    [Fact]
    public async Task InProcessExtensionsLoadBeforeFolderExtensionsWhenNeitherDependsOnTheOther()
    {
        var early = new DelegateExtension(builder => builder.AddProjectProfile(new StubProfile("fx.alpha.profile")));

        await using ExtensionHarness harness = await LoadAsync([Copy(FixtureExtensions.Alpha)], early);

        string[] expected = [BuiltInExtension.InProcessEntry.Manifest.Id, "harness.inprocess.0", FixtureExtensions.Alpha];
        Assert.Equal(expected, LoadedIds(harness.Registry));
        ExtensionContributionIssue issue = Assert.Single(harness.Registry.Issues);
        Assert.Equal(FixtureExtensions.Alpha, issue.ExtensionId);
    }

    [Fact]
    public async Task Npx007AMissingAssemblyFailsOnlyThatExtension()
    {
        string hollow = Path.Combine(root, "hollow");
        WriteManifest(hollow, ManifestJson("fx.hollow", "fx.hollow.dll"));

        await using ExtensionHarness harness = await LoadWithNeighboursAsync(hollow);

        Assert.Equal("NPX007", Failure(harness.Registry, "fx.hollow").Code);
        AssertNeighboursIntact(harness.Registry);
    }

    [Fact]
    public async Task TwoVersionsOfOneLibraryLoadTogetherAndEachTranslatorCallsItsOwn()
    {
        await using ExtensionHarness harness = await LoadAsync([Copy(FixtureExtensions.LibV1), Copy(FixtureExtensions.LibV2)]);

        ClassGraph cls = MultiExtensionGraphs.BuildClass(harness.Registry, "Ns", "Libs", "fx.libv1/Describe", "fx.libv2/Describe");
        string code = harness.Translate(cls);

        Assert.Contains("System.Console.WriteLine(\"shared-lib-v1\");", code, StringComparison.Ordinal);
        Assert.Contains("System.Console.WriteLine(\"shared-lib-v2:fx.libv2\");", code, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CurrentBehaviourAPrivateDependencyWhoseNameStartsWithNetPrintsIsLookedUpInTheHostAndTheExtensionFails()
    {
        await using ExtensionHarness harness = await LoadAsync([Copy(FixtureExtensions.PrefixedPrivate)]);

        ExtensionLoadResult.Failed failed = Failure(harness.Registry, FixtureExtensions.PrefixedPrivate);
        Assert.Equal("NPX005", failed.Code);
        Assert.Contains("NetPrintsFixture.Runtime", failed.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain(harness.Registry.NodeKinds, k => k.Kind == "fx.private-prefix/Describe");
    }

    [Fact]
    public async Task CurrentBehaviourAConsumerDoesNotSeeItsProvidersAssemblyAndFails()
    {
        await using ExtensionHarness harness = await LoadAsync([Copy(FixtureExtensions.TypesProvider), Copy(FixtureExtensions.TypesConsumer)]);

        Assert.Contains(FixtureExtensions.TypesProvider, LoadedIds(harness.Registry));
        ExtensionLoadResult.Failed failed = Failure(harness.Registry, FixtureExtensions.TypesConsumer);
        Assert.Equal("NPX005", failed.Code);
        Assert.Contains("Fx.TypesProvider", failed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CurrentBehaviourADiamondWhoseDependenciesProvideTheLibraryDoesNotSeeItAndFails()
    {
        await using ExtensionHarness harness = await LoadAsync(
            [Copy(FixtureExtensions.LibV1), Copy(FixtureExtensions.LibV2), Copy(FixtureExtensions.Diamond)]);

        Assert.Contains(FixtureExtensions.LibV1, LoadedIds(harness.Registry));
        Assert.Contains(FixtureExtensions.LibV2, LoadedIds(harness.Registry));
        ExtensionLoadResult.Failed failed = Failure(harness.Registry, FixtureExtensions.Diamond);
        Assert.Equal("NPX005", failed.Code);
        Assert.Contains("Fixture.SharedLib", failed.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANativeLibraryInTheExtensionFolderIsLoadedForItsManagedCode()
    {
        Assert.SkipUnless(OperatingSystem.IsLinux(), "The fixture ships the Linux native assets of SkiaSharp.");

        await using ExtensionHarness harness = await LoadAsync([Copy(FixtureExtensions.Native)]);

        Assert.Contains(FixtureExtensions.Native, LoadedIds(harness.Registry));
    }
}
