using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.Shell;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>FR-032, Clarifications answer 2: a user extension's application-level kinds join the app group once, its project-level kinds each session group.</summary>
public class ExtensionContributionRecorderTests
{
    private const string Id = "user.ext";

    private sealed class Noop : ICommandHandler
    {
        public bool CanExecute(ICommandContext context) => true;

        public Task ExecuteAsync(ICommandContext context, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class UserExtension : INetPrintsExtension
    {
        public void Register(IExtensionBuilder builder) =>
            builder.Editor
                .AddCommand(new CommandDescriptor("user.ext.global", "Global", new Noop()))
                .AddCommand(new CommandDescriptor("user.ext.graph", "Graph", new Noop(), Scope: CommandScope.Graph))
                .AddDashboardTile(new DashboardTileDescriptor("user.ext.tile", "Tile", 0, _ => new object()))
                .AddPanel(new PanelDescriptor("user.ext.panel", "Panel", _ => new object(), PanelDock.Left, 0))
                .AddStatusBarItem(new StatusBarItemDescriptor("user.ext.status", StatusBarAlignment.Right, 0, _ => new object()))
                .AddSettingsPage(new SettingsPageDescriptor("user.ext.settings", "Settings", null, 0));
    }

    private static (ExtensionHost Host, ExtensionContributionRecorder Recorder) Load()
    {
        var recorder = new ExtensionContributionRecorder();
        var manifest = new ExtensionManifest(Id, "User extension", "1.0.0", string.Empty, $"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}", []);
        var options = new ExtensionLoaderOptions([], [], [BuiltInExtension.InProcessEntry, (manifest, new UserExtension())]) { EditorContributions = recorder.Factory };
        return (new ExtensionHost(options, NullLoggerFactory.Instance), recorder);
    }

    [Fact]
    public async Task ApplicationLevelKindsJoinTheAppGroupOnce()
    {
        (ExtensionHost host, ExtensionContributionRecorder recorder) = Load();
        await using ExtensionHost _ = host;
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);

        recorder.ApplyToAppGroup(registry);
        recorder.ApplyToAppGroup(registry);
        registry.Freeze();

        Assert.Equal(["user.ext.global"], registry.Commands.Where(c => c.Id.StartsWith(Id, StringComparison.Ordinal) && !c.Id.Contains("showPanel", StringComparison.Ordinal)).Select(c => c.Id));
        Assert.Equal(["user.ext.tile"], registry.DashboardTiles.Select(t => t.Id));
        Assert.Equal(["user.ext.settings"], registry.SettingsPages.Select(p => p.Id));
        Assert.Empty(registry.Panels);
        Assert.Empty(registry.StatusBarItems);
        Assert.Equal("User extension", registry.OwnerNameOf("user.ext.tile"));
    }

    [Fact]
    public async Task ProjectLevelKindsJoinEachSessionGroupAndLeaveWithIt()
    {
        (ExtensionHost host, ExtensionContributionRecorder recorder) = Load();
        await using ExtensionHost _ = host;
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
        recorder.ApplyToAppGroup(registry);
        registry.Freeze();

        foreach (string key in new[] { "one", "two" })
        {
            registry.AddSessionGroup(key, group => recorder.PopulateSessionGroup(group, host.Current, []));

            Assert.Single(registry.Commands, c => c.Id == "user.ext.global");
            Assert.Single(registry.Commands, c => c.Id == "user.ext.graph");
            Assert.Single(registry.Panels, p => p.Id == "user.ext.panel");
            Assert.Single(registry.StatusBarItems, i => i.Id == "user.ext.status");
            Assert.Single(registry.DashboardTiles);
            Assert.DoesNotContain(registry.Issues, issue => issue.Kind == ContributionIssueKind.DuplicateId);

            registry.RemoveSessionGroup(key);

            Assert.Empty(registry.Panels);
            Assert.Empty(registry.StatusBarItems);
            Assert.DoesNotContain(registry.Commands, c => c.Id == "user.ext.graph");
            Assert.Single(registry.DashboardTiles);
        }
    }

    [Fact]
    public async Task AProjectsCopyOfAUserExtensionServesItsSession()
    {
        string temp = TestPaths.CreateTempDirectory();
        try
        {
            string userCopy = Path.Combine(temp, "netprints.test");
            Directory.CreateDirectory(userCopy);
            foreach (string file in Directory.GetFiles(TestExtensionFolder.Folder))
            {
                File.Copy(file, Path.Combine(userCopy, Path.GetFileName(file)));
            }

            await File.WriteAllTextAsync(
                Path.Combine(userCopy, "netprints-extension.json"),
                """{ "id": "netprints.test", "name": "User copy", "version": "0.9.0", "assembly": "NetPrints.TestExtension.dll", "netprintsApi": "0.4", "dependsOn": [] }""",
                TestContext.Current.CancellationToken);
            var recorder = new ExtensionContributionRecorder();
            await using var appHost = new ExtensionHost(new ExtensionLoaderOptions([temp], [], [BuiltInExtension.InProcessEntry]) { EditorContributions = recorder.Factory }, NullLoggerFactory.Instance);
            var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);
            recorder.ApplyToAppGroup(registry);
            registry.Freeze();

            Assert.Equal("User copy", registry.OwnerNameOf("netprints.test.tile"));

            await using var session = new ExtensionHost(new ExtensionLoaderOptions([temp], [TestExtensionFolder.Folder], [BuiltInExtension.InProcessEntry]) { EditorContributions = recorder.Factory }, NullLoggerFactory.Instance);

            Assert.Single(session.Current.Results.OfType<ExtensionLoadResult.Failed>(), failed => failed.Code == ExtensionDiagnosticCodes.Shadowed);

            registry.AddSessionGroup("project", group => recorder.PopulateSessionGroup(group, session.Current, [TestExtensionFolder.Folder]));

            Assert.DoesNotContain(registry.Issues, issue => issue.Kind == ContributionIssueKind.DuplicateId);
            Assert.Single(registry.Commands, c => c.Id == "netprints.test.hello");
            Assert.Single(registry.DashboardTiles, t => t.Id == "netprints.test.tile");
            Assert.Single(registry.SettingsPages, p => p.Id == "netprints.test.settings");
            Assert.Equal("NetPrints test extension", registry.OwnerNameOf("netprints.test.tile"));

            registry.RemoveSessionGroup("project");

            Assert.Equal("User copy", registry.OwnerNameOf("netprints.test.tile"));
            Assert.Single(registry.DashboardTiles, t => t.Id == "netprints.test.tile");
        }
        finally
        {
            TestPaths.TryDelete(temp);
        }
    }
}
