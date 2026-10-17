using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Extensibility.Loading;
using NetPrints.Testing;

namespace NetPrints.Editor.Tests.Contributions;

/// <summary>FR-034, FR-035, research R27: an extension's failure leaves the editor usable and names the extension.</summary>
public sealed class FailureIsolationTests : IAsyncDisposable
{
    private const string ExtensionName = "Faulty fixture extension";
    private const string GroupKey = "project";

    private readonly CollectingLogger<ContributionRegistry> logger = new();
    private readonly ExtensionContributionRecorder recorder = new();
    private readonly List<ExtensionHost> hosts = [];

    private sealed class Contexts : ICommandContextProvider
    {
        public event EventHandler? CommandStatesChanged
        {
            add { }
            remove { }
        }

        public CommandContext Create(object? parameter = null, CommandScope scope = CommandScope.Global) => new(new FakeShell(), null, null, null, CommandSelection.None, parameter, scope);
    }

    public async ValueTask DisposeAsync()
    {
        foreach (ExtensionHost host in hosts)
        {
            await host.DisposeAsync();
        }
    }

    private ContributionRegistry Load(string folder)
    {
        var host = new ExtensionHost(new ExtensionLoaderOptions([], [folder], []) { EditorContributions = recorder.Factory }, NullLoggerFactory.Instance);
        hosts.Add(host);
        var registry = new ContributionRegistry(logger);
        registry.Freeze();
        registry.AddSessionGroup(GroupKey, group => recorder.PopulateSessionGroup(group, host.Current, [folder]));
        return registry;
    }

    [Fact]
    public async Task AThrowingHandlerRaisesAnExceptionNamingTheExtensionAndTheCommand()
    {
        ContributionRegistry registry = Load(FixtureExtensions.FaultyFolder());
        var faulted = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
        var invoker = new CommandInvoker(registry, new Contexts(), faulted.SetResult);
        CommandDescriptor command = registry.Commands.Single(c => c.Id == "fx.faulty.throws");

        Assert.True(invoker.TryRun(command));

        Exception fault = await faulted.Task.WaitAsync(TestContext.Current.CancellationToken);
        Assert.IsType<ExtensionFailureException>(fault);
        Assert.Contains(ExtensionName, fault.Message, StringComparison.Ordinal);
        Assert.Contains("fx.faulty.throws", fault.Message, StringComparison.Ordinal);
        Assert.IsType<InvalidOperationException>(fault.InnerException);
    }

    [Fact]
    public void AThrowingTooltipProviderLosesOnlyItsOwnResultAndIsLoggedOncePerSession()
    {
        ContributionRegistry registry = Load(FixtureExtensions.FaultyFolder());
        var target = new TooltipTarget(TooltipTargetKind.Pin, new object());

        TooltipContent? first = TooltipResolver.Resolve(registry.TooltipProviders, target);
        TooltipContent? second = TooltipResolver.Resolve(registry.TooltipProviders, target);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Single(logger.Entries, entry => entry.EventId.Id == 1071);
    }

    [Fact]
    public async Task AThrowingGoToProviderGivesNoResultsAndDoesNotThrow()
    {
        ContributionRegistry registry = Load(FixtureExtensions.FaultyFolder());
        IGoToProvider provider = registry.GoToProviders.Single();

        List<GoToItem> items = [];
        await foreach (GoToItem item in provider.SearchAsync("x", TestContext.Current.CancellationToken))
        {
            items.Add(item);
        }

        Assert.Empty(items);
        Assert.Single(logger.Entries, entry => entry.EventId.Id == 1071);
    }

    [Fact]
    public void ADuplicateIdAGestureConflictAndAnUnknownReferenceAreReportedAndLogged()
    {
        ContributionRegistry registry = Load(FixtureExtensions.FaultyFolder());

        Assert.Contains(registry.Issues, issue => issue.Kind == ContributionIssueKind.DuplicateId && issue.Id == "fx.faulty.duplicate" && issue.Owner == "fx.faulty");
        Assert.Contains(registry.Issues, issue => issue.Kind == ContributionIssueKind.GestureConflict && issue.Id == "fx.faulty.conflict");
        Assert.Contains(registry.Issues, issue => issue.Kind == ContributionIssueKind.UnknownReference && issue.Id == "fx.faulty.menu");
        Assert.Contains(logger.Entries, entry => entry.EventId.Id == 1070 && entry.Message.Contains("UnknownReference", StringComparison.Ordinal));
        Assert.Single(registry.Commands, command => command.Id == "fx.faulty.duplicate");
    }

    [Fact]
    public async Task AThrowingRegisterContributesNothingAndFailsWithNpx005()
    {
        string folder = FixtureExtensions.RegisterFaultFolder();
        var host = new ExtensionHost(new ExtensionLoaderOptions([], [folder], []) { EditorContributions = recorder.Factory }, NullLoggerFactory.Instance);
        await using ExtensionHost extensions = host;
        var registry = new ContributionRegistry(logger);
        registry.Freeze();

        registry.AddSessionGroup(GroupKey, group => recorder.PopulateSessionGroup(group, host.Current, [folder]));

        ExtensionLoadResult.Failed failure = Assert.Single(host.Current.Results.OfType<ExtensionLoadResult.Failed>());
        Assert.Equal(ExtensionDiagnosticCodes.RegisterFailed, failure.Code);
        Assert.Equal("fx.registerfault", failure.Id);
        Assert.DoesNotContain(registry.Commands, command => command.Id == "fx.registerfault.hello");
    }

    [Fact]
    public async Task TheErrorsDialogListsEveryRegistryIssueWithItsNpxCodeAndTheExtensionName()
    {
        await using var editor = TestEditor.Create(TestEditor.CreateReflectionHost, extensions: NewHost(), recorder: recorder);
        string csproj = TestPaths.CopyHelloWorldSample();
        try
        {
            var snapshot = await editor.Projects.LoadAsync(csproj, TestContext.Current.CancellationToken);
            editor.Projects.Seed(snapshot with { ExtensionFolders = [FixtureExtensions.FaultyFolder()] });
            await editor.Settings.SetAsync(NetPrints.Extensibility.Settings.NetPrintsSettings.Descriptor,
                NetPrints.Extensibility.Settings.NetPrintsSettings.Empty with { TrustedProjects = [csproj] }, TestContext.Current.CancellationToken);
            using var rig = new ProjectRig(editor.Context);

            await rig.LoadProjectAsync(csproj);

            IReadOnlyList<NetPrints.Compilation.CodeDiagnostic> rows = Assert.Single(editor.Dialogs.IssueDialogs).Issues;
            Assert.Contains(rows, row => row.Id == "NPX004" && row.Message.Contains(ExtensionName, StringComparison.Ordinal));
            Assert.Contains(rows, row => row.Id == "NPX009" && row.Message.Contains(ExtensionName, StringComparison.Ordinal));
            Assert.Contains(rows, row => row.Id == "NPX010" && row.Message.Contains(ExtensionName, StringComparison.Ordinal));
            Assert.NotNull(rig.Project);
        }
        finally
        {
            TestPaths.TryDelete(Path.GetDirectoryName(csproj) ?? csproj);
        }
    }

    private ExtensionHost NewHost()
    {
        var host = new ExtensionHost(new ExtensionLoaderOptions([], [], [NetPrints.Extensibility.BuiltInExtension.InProcessEntry]) { EditorContributions = recorder.Factory }, NullLoggerFactory.Instance);
        return host;
    }
}
