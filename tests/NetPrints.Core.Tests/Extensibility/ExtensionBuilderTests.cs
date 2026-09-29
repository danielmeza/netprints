using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;
using NetPrints.Extensibility.Settings;
using NetPrints.Reflection;
using NetPrints.Translator;
using Xunit;
using static NetPrints.Tests.Extensibility.ExtensionTestSupport;

namespace NetPrints.Tests.Extensibility;

/// <summary>The buffered <see cref="IExtensionBuilder"/> (extension-points.md §1).</summary>
public class ExtensionBuilderTests
{
    [Fact]
    public async Task ContributionsAreCommittedWhenRegisterReturns()
    {
        var emitter = new NamedClassEmitter("c");
        var memberEmitter = new DisposableMemberEmitter();
        var profile = new StubProfile("test.profile");
        var catalog = new InMemoryTypeCatalog(new CatalogInfo("cat", "1", []), [], [], [], [], new Dictionary<TypeSpecifier, IReadOnlyList<string>>(), new Dictionary<MethodSpecifier, string>());
        var extension = InProcess("test.ext", builder => builder
            .AddClassEmitter(emitter)
            .AddMemberEmitter(memberEmitter)
            .AddProjectProfile(profile)
            .AddTypeCatalog(catalog)
            .AddNodeLibrary(new SingleKindLibrary("test.ext", PingKind("test.ext/Ping")))
            .AddProjectProperty("NetPrintsTestMode")
            .AddProjectProperty("netprintstestmode")
            .AddProjectProperty("Other"));

        await using ExtensionRegistry registry = Load(Options([extension]));

        Assert.Same(emitter, Assert.Single(registry.ClassEmitters));
        Assert.Same(memberEmitter, Assert.Single(registry.MemberEmitters));
        Assert.Same(catalog, Assert.Single(registry.TypeCatalogs));
        Assert.Equal([DefaultProjectProfile.ProfileId, "test.profile"], registry.Profiles.Select(p => p.Id));
        Assert.Same(profile, registry.FindProfile("test.profile"));
        Assert.Null(registry.FindProfile("missing"));
        Assert.Equal("test.ext/Ping", Assert.Single(registry.NodeKinds).Kind);
        Assert.Equal(["NetPrintsTestMode", "Other"], registry.ProjectProperties);
        Assert.Same(emitter, Assert.Single(registry.Translation.ClassEmitters));
        Assert.Same(memberEmitter, Assert.Single(registry.Translation.MemberEmitters));
        Assert.NotNull(registry.Translation.Nodes.Find(typeof(PingNode)));
        Assert.NotNull(registry.NodeConverters.FindByKind("test.ext/Ping"));
        Assert.Empty(registry.Issues);
    }

    [Fact]
    public async Task ARegisterThatThrowsDiscardsAllItsContributions()
    {
        var extension = InProcess("test.ext", builder =>
        {
            builder.AddClassEmitter(new NamedClassEmitter("c")).AddProjectProperty("P");
            throw new InvalidOperationException("boom");
        });
        var good = InProcess("test.good", builder => builder.AddClassEmitter(new NamedClassEmitter("good")));

        await using ExtensionRegistry registry = Load(Options([extension, good]));

        Assert.Equal("good", Assert.Single(registry.ClassEmitters).Id);
        Assert.Empty(registry.ProjectProperties);
        var failure = SingleFailure(registry, "test.ext");
        Assert.Equal("NPX005", failure.Code);
        Assert.IsType<InvalidOperationException>(failure.Exception);
        Assert.Equal(["test.good"], registry.Loaded.Select(m => m.Id));
    }

    [Fact]
    public async Task NullArgumentsThrowArgumentNullException()
    {
        var extension = InProcess("test.ext", builder =>
        {
            AssertAddThrowsForNullArgument(builder, nameof(IExtensionBuilder.AddNodeLibrary));
            AssertAddThrowsForNullArgument(builder, nameof(IExtensionBuilder.AddClassEmitter));
            AssertAddThrowsForNullArgument(builder, nameof(IExtensionBuilder.AddMemberEmitter));
            AssertAddThrowsForNullArgument(builder, nameof(IExtensionBuilder.AddTypeCatalog));
            AssertAddThrowsForNullArgument(builder, nameof(IExtensionBuilder.AddProjectProfile));
            AssertAddThrowsForNullArgument(builder, nameof(IExtensionBuilder.AddJsonTypeInfoResolver));
            AssertAddThrowsForNullArgument(builder, nameof(IExtensionBuilder.AddProjectProperty));
            Assert.Throws<ArgumentException>(() => builder.AddProjectProperty(" "));
            Assert.Equal("test.ext", builder.Manifest.Id);
            Assert.NotNull(builder.LoggerFactory);
        });

        await using ExtensionRegistry registry = Load(Options([extension]));

        Assert.Equal("test.ext", Assert.Single(registry.Loaded).Id);
    }

    /// <summary>
    /// Invokes <paramref name="methodName"/> on <see cref="IExtensionBuilder"/> with a null argument
    /// through reflection — its parameters are non-nullable, so a literal <see langword="null"/> (or a
    /// null-forgiving one) would not compile — and asserts the call throws <see cref="ArgumentNullException"/>.
    /// </summary>
    private static void AssertAddThrowsForNullArgument(IExtensionBuilder builder, string methodName)
    {
        MethodInfo method = typeof(IExtensionBuilder).GetMethod(methodName)
            ?? throw new InvalidOperationException($"No method named '{methodName}' on {nameof(IExtensionBuilder)}.");

        TargetInvocationException thrown = Assert.Throws<TargetInvocationException>(() => method.Invoke(builder, [null]));

        Assert.IsType<ArgumentNullException>(thrown.InnerException);
    }

    [Fact]
    public async Task BuilderCallsAfterRegisterThrowInvalidOperationException()
    {
        IExtensionBuilder? captured = null;
        var extension = InProcess("test.ext", builder => captured = builder);

        await using ExtensionRegistry registry = Load(Options([extension]));

        Assert.NotNull(captured);
        Assert.Throws<InvalidOperationException>(() => captured.AddClassEmitter(new NamedClassEmitter("late")));
        Assert.Throws<InvalidOperationException>(() => captured.AddProjectProperty("Late"));
        Assert.Empty(registry.ClassEmitters);
    }

    [Fact]
    public async Task DisposingTheRegistryDisposesContributions()
    {
        var emitter = new DisposableMemberEmitter();
        var extension = InProcess("test.ext", builder => builder.AddMemberEmitter(emitter));
        ExtensionRegistry registry = Load(Options([extension]));

        Assert.False(emitter.Disposed);
        await registry.DisposeAsync();
        await registry.DisposeAsync();
        Assert.True(emitter.Disposed);
    }

    // R1-10: a contribution implementing both IDisposable and IAsyncDisposable is awaited through the
    // async path, not disposed synchronously.
    [Fact]
    public async Task DisposingARegistryPrefersIAsyncDisposableOverIDisposable()
    {
        var emitter = new DualDisposableMemberEmitter();
        var extension = InProcess("test.ext", builder => builder.AddMemberEmitter(emitter));
        ExtensionRegistry registry = Load(Options([extension]));

        await registry.DisposeAsync();

        Assert.True(emitter.AsyncDisposed);
        Assert.False(emitter.SyncDisposed);
    }

    private sealed class StubHostChannelFactory(string id) : IHostChannelFactory
    {
        public string Id => id;

        public IHostChannel Create(HostLaunchContext context) => NullHostChannel.Instance;
    }

    private static ExtensionSettingsDescriptor<NetPrintsSettings> SettingsFor(string id) =>
        NetPrintsSettings.Descriptor with { ExtensionId = id };

    [Fact]
    public async Task HostChannelsAndSettingsAreCommittedAndFound()
    {
        var factory = new StubHostChannelFactory("test");
        ExtensionSettingsDescriptor<NetPrintsSettings> descriptor = SettingsFor("test.ext");
        var extension = InProcess("test.ext", builder => builder.AddHostChannel(factory).AddSettings(descriptor));

        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry, extension]));

        Assert.Same(factory, Assert.Single(registry.HostChannels));
        Assert.Same(factory, registry.FindHostChannel("test"));
        Assert.Null(registry.FindHostChannel("missing"));
        Assert.Equal(["netprints", "test.ext"], registry.Settings.Select(s => s.ExtensionId));
        Assert.Same(descriptor, registry.Settings[1]);
        Assert.Empty(registry.Issues);
    }

    [Fact]
    public async Task DuplicateHostChannelIdsAndSettingsSectionsAreNpx006()
    {
        var extension = InProcess("test.ext", builder => builder
            .AddHostChannel(new StubHostChannelFactory("dup"))
            .AddHostChannel(new StubHostChannelFactory("dup"))
            .AddSettings(SettingsFor("test.ext"))
            .AddSettings(SettingsFor("test.ext"))
            .AddSettings(SettingsFor("someone.else")));
        var other = InProcess("test.other", builder => builder.AddHostChannel(new StubHostChannelFactory("dup")));

        await using ExtensionRegistry registry = Load(Options([extension, other]));

        Assert.Single(registry.HostChannels);
        Assert.Equal("test.ext", Assert.Single(registry.Settings).ExtensionId);
        Assert.Equal(4, registry.Issues.Count);
        Assert.All(registry.Issues, issue => Assert.Equal(ExtensionDiagnosticCodes.ContributionRejected, issue.Code));
        Assert.Equal(2, registry.Loaded.Count);
    }

    [Fact]
    public async Task BuiltInExtensionDeclaresTheNetPrintsSection()
    {
        await using ExtensionRegistry registry = Load(Options([BuiltInExtension.InProcessEntry]));

        Assert.Same(NetPrintsSettings.Descriptor, Assert.Single(registry.Settings));
    }
}
