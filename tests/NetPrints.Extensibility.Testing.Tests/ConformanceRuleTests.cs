using System.Text.Json;
using Fx.Kit;
using Microsoft.Extensions.DependencyInjection;
using NetPrints.Core;
using NetPrints.Extensibility.Editor;
using NetPrints.Extensibility.Hosting;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Nodes;

namespace NetPrints.Extensibility.Testing.Tests;

/// <summary>FR-076, FR-077: the rules of the checks that a failing fixture does not reach, driven with extensions made in the test.</summary>
public sealed class ConformanceRuleTests
{
    private const string Id = "test.rules";

    private static async Task<ConformanceCheckResult> RunAsync(INetPrintsExtension extension, string code, ConformanceOptions? options = null)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        string api = $"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}";
        await using ExtensionHarness harness = await ExtensionHarness.CreateWithManifestsAsync(
            [], [(new ExtensionManifest(Id, Id, "1.0.0", Id + ".dll", api, []), extension)], cancellationToken);
        return await ConformanceSuite.RunCheckAsync(harness, options ?? new ConformanceOptions(), code, cancellationToken);
    }

    private static string Messages(ConformanceCheckResult result) => string.Join('\n', result.Findings.Select(finding => finding.ToString()));

    [Fact]
    public async Task AnEditorContributionThatFollowsTheRulesPasses()
    {
        ConformanceCheckResult result = await RunAsync(new EditorExtension(Id + ".run", "Run", Id + ".mark", [CommandGesture.Parse("Ctrl+R", null)], CommandScope.Global), NpkDiagnosticCodes.EditorContributions);

        Assert.Equal(ConformanceStatus.Passed, result.Status);
    }

    [Theory]
    [InlineData("A", CommandScope.Global, "no modifier")]
    [InlineData("Ctrl+R", CommandScope.Graph, "")]
    [InlineData("F5", CommandScope.Global, "")]
    [InlineData("A", CommandScope.Graph, "")]
    public async Task AGestureWithoutAModifierNeedsANarrowerScopeThanGlobal(string gesture, CommandScope scope, string expected)
    {
        ConformanceCheckResult result = await RunAsync(new EditorExtension(Id + ".run", "Run", null, [CommandGesture.Parse(gesture, null)], scope), NpkDiagnosticCodes.EditorContributions);

        Assert.Equal(expected.Length == 0 ? ConformanceStatus.Passed : ConformanceStatus.Failed, result.Status);
        Assert.Contains(expected, Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ALabelWithoutAKeyOrADefaultTextIsReported()
    {
        ConformanceCheckResult result = await RunAsync(new EditorExtension(Id + ".run", new LocalizedText("Run.Label"), null, [], CommandScope.Global), NpkDiagnosticCodes.EditorContributions);

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("Run.Label", Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnIconIdOfAnotherExtensionIsReported()
    {
        ConformanceCheckResult result = await RunAsync(new EditorExtension(Id + ".run", "Run", "other.mark", [], CommandScope.Global), NpkDiagnosticCodes.EditorContributions);

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("other.mark", Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnIconThatNoViewSourceDefinesIsReported()
    {
        string directory = Directory.CreateTempSubdirectory("npk015").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "Icons.axaml"), "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><StreamGeometry x:Key=\"Icon.test.rules.other\">M0,0</StreamGeometry></ResourceDictionary>", TestContext.Current.CancellationToken);

            ConformanceCheckResult result = await RunAsync(
                new EditorExtension(Id + ".run", "Run", Id + ".mark", [], CommandScope.Global), NpkDiagnosticCodes.EditorContributions, new ConformanceOptions { SourceDirectory = directory });

            Assert.Equal(ConformanceStatus.Failed, result.Status);
            Assert.Contains("Icon.test.rules.mark", Messages(result), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task ASingletonThatCapturesAScopedServiceFailsTheProviderRule()
    {
        ConformanceCheckResult result = await RunAsync(new ServicesExtension(captive: true), NpkDiagnosticCodes.Services);

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("builds, validates", Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AServiceThatReplacesAHostExportFailsTheProviderRule()
    {
        ConformanceCheckResult result = await RunAsync(new ServicesExtension(captive: false), NpkDiagnosticCodes.Services);

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("TimeProvider", Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AScopedServiceThatIsReleasedPassesTheLeakTest()
    {
        ConformanceCheckResult result = await RunAsync(new ServicesExtension(captive: null), NpkDiagnosticCodes.Services);

        Assert.True(result.Status == ConformanceStatus.Passed, Messages(result));
    }

    [Fact]
    public async Task ViewsThatUseTokensAndThemeDictionariesPass()
    {
        string directory = Directory.CreateTempSubdirectory("npk016").FullName;
        try
        {
            await File.WriteAllTextAsync(
                Path.Combine(directory, "Good.axaml"),
                "<UserControl xmlns=\"https://github.com/avaloniaui\"><Border Background=\"{DynamicResource Panel.Background}\" BorderBrush=\"Transparent\"><TextBlock Classes=\"title\" FontSize=\"{DynamicResource Font.Title}\" /></Border></UserControl>",
                TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                Path.Combine(directory, "Tokens.axaml"),
                "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><ResourceDictionary.ThemeDictionaries><ResourceDictionary x:Key=\"Dark\"><SolidColorBrush x:Key=\"Mine\" Color=\"#202020\" /></ResourceDictionary></ResourceDictionary.ThemeDictionaries></ResourceDictionary>",
                TestContext.Current.CancellationToken);

            ConformanceCheckResult result = await RunAsync(new EditorExtension(Id + ".run", "Run", null, [], CommandScope.Global), NpkDiagnosticCodes.ContributedViews, new ConformanceOptions { SourceDirectory = directory });

            Assert.True(result.Status == ConformanceStatus.Passed, Messages(result));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("Styles.axaml", "<Styles xmlns=\"https://github.com/avaloniaui\"><Style Selector=\"Button\"><Setter Property=\"Background\" Value=\"#FF0000\" /></Style></Styles>", "Setter Background=#FF0000")]
    [InlineData("Fonts.axaml", "<Styles xmlns=\"https://github.com/avaloniaui\"><Style Selector=\"TextBlock\"><Setter Property=\"FontSize\" Value=\"18\" /></Style></Styles>", "Setter FontSize=18")]
    [InlineData("Brushes.axaml", "<ResourceDictionary xmlns=\"https://github.com/avaloniaui\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><SolidColorBrush x:Key=\"Mine\">#00FF00</SolidColorBrush></ResourceDictionary>", "SolidColorBrush holds the colour #00FF00")]
    public async Task AStylesOrResourceDictionaryFileWithALiteralFails(string fileName, string content, string expected)
    {
        string directory = Directory.CreateTempSubdirectory("npk016").FullName;
        try
        {
            await File.WriteAllTextAsync(Path.Combine(directory, fileName), content, TestContext.Current.CancellationToken);

            ConformanceCheckResult result = await RunAsync(new EditorExtension(Id + ".run", "Run", null, [], CommandScope.Global), NpkDiagnosticCodes.ContributedViews, new ConformanceOptions { SourceDirectory = directory });

            Assert.Equal(ConformanceStatus.Failed, result.Status);
            Assert.Contains(expected, Messages(result), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData(NpkDiagnosticCodes.NodeRoundTrip, false)]
    [InlineData(NpkDiagnosticCodes.NodeRoundTrip, true)]
    [InlineData(NpkDiagnosticCodes.TranslationCompiles, false)]
    [InlineData(NpkDiagnosticCodes.TranslationCompiles, true)]
    [InlineData(NpkDiagnosticCodes.DeterministicEmitters, false)]
    [InlineData(NpkDiagnosticCodes.DeterministicEmitters, true)]
    public async Task AKindWithoutANodeSearchEntryMakesTheNodeChecksSkipAndNamesIt(string code, bool withEntry)
    {
        ConformanceCheckResult result = await RunAsync(new KindsExtension(withEntry), code);

        Assert.Equal(ConformanceStatus.Skipped, result.Status);
        Assert.Contains(Id + "/Hidden", result.Note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChannelThatTakesAMessageAfterItClosedFails()
    {
        ConformanceCheckResult result = await RunAsync(new LenientChannelExtension(), NpkDiagnosticCodes.HostChannel);

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("did not throw InvalidOperationException", Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChannelWhoseMessagesNeverCompleteFails()
    {
        ConformanceCheckResult result = await RunAsync(new SilentChannelExtension(), NpkDiagnosticCodes.HostChannel);

        Assert.Equal(ConformanceStatus.Failed, result.Status);
        Assert.Contains("silent: Messages did not complete within 5 seconds of the channel closing", Messages(result), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AChannelThatCompletesItsMessagesOnAnotherThreadAfterClosingPasses()
    {
        ConformanceCheckResult result = await RunAsync(new LateChannelExtension(), NpkDiagnosticCodes.HostChannel);

        Assert.Equal(ConformanceStatus.Passed, result.Status);
    }

    [Fact]
    public async Task ASettingsSectionThatPassesIsExercised()
    {
        string folder = KitFixtures.AlphaFolder();
        await using ExtensionHarness harness = await ExtensionHarness.CreateAsync([folder], [], TestContext.Current.CancellationToken);

        ConformanceCheckResult result = await ConformanceSuite.RunCheckAsync(harness, new ConformanceOptions(), NpkDiagnosticCodes.Settings, TestContext.Current.CancellationToken);

        Assert.Equal(ConformanceStatus.Passed, result.Status);
        Assert.Empty(result.Note);
    }

    private sealed class KindsExtension(bool withEntry) : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.AddNodeLibrary(new FxNodeLibrary(Id + ".hidden", FxKit.Kind(Id + "/Hidden", () => "hidden") with { Suggestions = [] }));
            if (withEntry)
            {
                builder.AddNodeLibrary(new FxNodeLibrary(Id + ".shown", FxKit.Kind(Id + "/Shown", () => "shown")));
            }

            builder.AddJsonTypeInfoResolver(FxKitJsonContext.Default);
        }
    }

    private sealed class LenientChannelExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.AddHostChannel(new LenientFactory());
        }
    }

    private sealed class LenientFactory : IHostChannelFactory
    {
        public string Id => "lenient";

        public IHostChannel Create(HostLaunchContext context) => new LenientChannel(InMemoryHostChannel.CreatePair("lenient").Editor);
    }

    private sealed class LenientChannel(IHostChannel inner) : IHostChannel
    {
        public string Id => inner.Id;

        public HostChannelState State => inner.State;

        public IObservable<HostMessage> Messages => inner.Messages;

        public ValueTask SendAsync(HostMessage message, CancellationToken cancellationToken) => ValueTask.CompletedTask;

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class SilentChannelExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.AddHostChannel(new SilentFactory());
        }
    }

    private sealed class SilentFactory : IHostChannelFactory
    {
        public string Id => "silent";

        public IHostChannel Create(HostLaunchContext context) => new SilentChannel(InMemoryHostChannel.CreatePair("silent").Editor);
    }

    private sealed class SilentChannel(IHostChannel inner) : IHostChannel
    {
        public string Id => inner.Id;

        public HostChannelState State => inner.State;

        public IObservable<HostMessage> Messages { get; } = new SilentMessages();

        public ValueTask SendAsync(HostMessage message, CancellationToken cancellationToken) => inner.SendAsync(message, cancellationToken);

        public ValueTask DisposeAsync() => inner.DisposeAsync();
    }

    private sealed class SilentMessages : IObservable<HostMessage>
    {
        public IDisposable Subscribe(IObserver<HostMessage> observer) => new EmptySubscription();
    }

    private sealed class LateChannelExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.AddHostChannel(new LateFactory());
        }
    }

    private sealed class LateFactory : IHostChannelFactory
    {
        public string Id => "late";

        public IHostChannel Create(HostLaunchContext context) => new LateChannel(InMemoryHostChannel.CreatePair("late").Editor);
    }

    private sealed class LateChannel(IHostChannel inner) : IHostChannel
    {
        private readonly LateMessages messages = new();

        public string Id => inner.Id;

        public HostChannelState State => inner.State;

        public IObservable<HostMessage> Messages => messages;

        public ValueTask SendAsync(HostMessage message, CancellationToken cancellationToken) => inner.SendAsync(message, cancellationToken);

        public ValueTask DisposeAsync()
        {
            messages.CompleteOnAnotherThread();
            return inner.DisposeAsync();
        }
    }

    private sealed class LateMessages : IObservable<HostMessage>
    {
        private const int DelayMilliseconds = 50;

        private readonly Lock gate = new();
        private readonly List<IObserver<HostMessage>> observers = [];

        public IDisposable Subscribe(IObserver<HostMessage> observer)
        {
            lock (gate)
            {
                observers.Add(observer);
            }

            return new EmptySubscription();
        }

        public void CompleteOnAnotherThread()
        {
            var thread = new Thread(() =>
            {
                Thread.Sleep(DelayMilliseconds);
                IObserver<HostMessage>[] current;
                lock (gate)
                {
                    current = [.. observers];
                    observers.Clear();
                }

                foreach (IObserver<HostMessage> observer in current)
                {
                    observer.OnCompleted();
                }
            })
            {
                IsBackground = true,
            };
            thread.Start();
        }
    }

    private sealed class EmptySubscription : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

/// <summary>An extension with one command.</summary>
public sealed class EditorExtension(string commandId, LocalizedText label, string? iconId, IReadOnlyList<CommandGesture> gestures, CommandScope scope) : INetPrintsExtension
{
    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Editor.AddCommand(new CommandDescriptor(commandId, label, new NoopHandler(), iconId, gestures, scope));
    }
}

/// <summary>A command that does nothing.</summary>
public sealed class NoopHandler : ICommandHandler
{
    /// <inheritdoc />
    public bool CanExecute(ICommandContext context) => true;

    /// <inheritdoc />
    public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>A scoped service.</summary>
public sealed class ScopedDependency;

/// <summary>A singleton that needs a scoped service.</summary>
/// <param name="dependency">The scoped service.</param>
public sealed class CaptiveSingleton(ScopedDependency dependency)
{
    /// <summary>Gets the captured service.</summary>
    public ScopedDependency Dependency { get; } = dependency;
}

/// <summary>An extension that registers services in one of three ways.</summary>
public sealed class ServicesExtension(bool? captive) : INetPrintsExtension
{
    /// <inheritdoc />
    public void Register(IExtensionBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.Services.AddScoped<ScopedDependency>();
        switch (captive)
        {
            case true:
                builder.Services.AddSingleton<CaptiveSingleton>();
                break;
            case false:
                builder.Services.AddSingleton(TimeProvider.System);
                break;
        }
    }
}
