using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Contributions;
using NetPrints.Editor.Hosting;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Loading;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>SC-003: the Godot sample extension, built against the packed packages, loads in the editor host from an extension directory with no extension diagnostic. CI sets <see cref="DirectoryVariable"/> to the unpacked extension package.</summary>
public class GodotExtensionHostTests
{
    private const string DirectoryVariable = "NETPRINTS_GODOT_EXTENSION_DIR";
    private const string Id = "netprints.godot";

    [Fact]
    public async Task TheBuiltExtensionLoadsFromAnExtensionDirectoryWithNoDiagnostic()
    {
        string? directory = Environment.GetEnvironmentVariable(DirectoryVariable);
        Assert.SkipWhen(string.IsNullOrEmpty(directory), $"{DirectoryVariable} names the directory that holds the built Godot extension");
        var recorder = new ExtensionContributionRecorder();
        var options = new ExtensionLoaderOptions([directory ?? string.Empty], [], [BuiltInExtension.InProcessEntry]) { EditorContributions = recorder.Factory };
        await using var host = new ExtensionHost(options, NullLoggerFactory.Instance);
        var registry = new ContributionRegistry(NullLogger<ContributionRegistry>.Instance);

        recorder.ApplyToAppGroup(registry);
        registry.Freeze();
        registry.AddSessionGroup("godot", group => recorder.PopulateSessionGroup(group, host.Current, []));

        Assert.Empty(host.Current.Results.OfType<ExtensionLoadResult.Failed>());
        Assert.Contains(host.Current.Results.OfType<ExtensionLoadResult.Loaded>(), result => result.Id == Id);
        Assert.Empty(host.Current.Issues);
        Assert.Empty(registry.Issues);
        Assert.NotNull(host.Current.FindProfile("godot"));
        Assert.Contains(host.Current.ProjectTemplates, template => template.ProfileId == "godot");
        Assert.Contains(registry.Commands, command => command.Id == $"{Id}.checkSetup");
        Assert.Contains(registry.StatusBarItems, item => item.Id == $"{Id}.status");
        Assert.Contains(registry.SettingsPages, page => page.Id == $"{Id}.settings");
    }
}
