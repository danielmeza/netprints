using System.Text.Json.Nodes;
using NetPrints.Core;
using NetPrints.Extensibility;
using NetPrints.Extensibility.Editor;
using NetPrints.Extensibility.Loading;
using NetPrints.Extensibility.Settings;
using NetPrints.Extensibility.Testing;
using NetPrints.Projects;

namespace NetPrints.Godot.Tests;

public sealed class GodotContributionTests
{
    private static async Task<ExtensionHarness> LoadAsync()
    {
        var manifest = new ExtensionManifest(GodotExtension.Id, "NetPrints for Godot", "0.1.0", "NetPrints.Godot.dll", $"{ExtensionApi.Version.Major}.{ExtensionApi.Version.Minor}", []);
        return await ExtensionHarness.CreateWithManifestsAsync([], [(manifest, new GodotExtension())], TestContext.Current.CancellationToken);
    }

    private static IEnumerable<T> Recorded<T>(ExtensionHarness harness) =>
        harness.EditorContributions.Where(item => item.ExtensionId == GodotExtension.Id).Select(item => item.Descriptor).OfType<T>();

    [Fact]
    public async Task TheGodotProfileIsRegistered()
    {
        await using ExtensionHarness harness = await LoadAsync();

        IProjectProfile profile = Assert.IsAssignableFrom<IProjectProfile>(harness.Registry.FindProfile(GodotProfile.ProfileId));

        Assert.Equal("godot", profile.Id);
        Assert.Contains("Godot.NET.Sdk", profile.ProjectTemplate, StringComparison.Ordinal);
        Assert.Contains("NetPrints.Sdk", profile.ProjectTemplate, StringComparison.Ordinal);
        Assert.Contains(profile.BaseTypes, type => type.ToString().Contains("Godot.Node", StringComparison.Ordinal));
    }

    [Fact]
    public async Task NewGraphClassesArePartialSoGodotCanAttachTheirScripts()
    {
        await using ExtensionHarness harness = await LoadAsync();
        IProjectProfile profile = Assert.IsAssignableFrom<IProjectProfile>(harness.Registry.FindProfile(GodotProfile.ProfileId));
        ClassTemplate template = Assert.Single(profile.ClassTemplates);

        var snapshot = new ProjectSnapshot("Game.csproj", "Game", "Game", "Game", BinaryType.SharedLibrary, "net10.0", GodotProfile.ProfileId, ReferencesNetPrintsSdk: true,
            GraphFiles: [], ExtensionFolders: [], References: [], DeclaredReferences: [], OtherSources: [], CompilationOptionsJson: "{}", Properties: new Dictionary<string, string>(), Messages: []);

        ClassGraph graph = template.Create(Project.FromSnapshot(snapshot), "Ship");

        Assert.True(graph.Modifiers.HasFlag(ClassModifiers.Partial));
        Assert.Equal("Ship", graph.Name);
        Assert.Equal("Game", graph.Namespace);
    }

    [Fact]
    public async Task TheTemplateOffersARunnableGodotProjectWithBothLaunchProfiles()
    {
        await using ExtensionHarness harness = await LoadAsync();

        ProjectTemplateDescriptor template = Assert.Single(harness.Registry.ProjectTemplates);
        TemplateFile launch = Assert.Single(template.Files ?? [], file => file.Path == "Properties/launchSettings.json");
        JsonObject profiles = Assert.IsType<JsonObject>(Assert.IsType<JsonObject>(JsonNode.Parse(launch.Content))["profiles"]);

        Assert.Equal(GodotProfile.ProfileId, template.ProfileId);
        Assert.Equal(ProjectOutputType.Library, template.OutputType);
        Assert.Equal(["Game (Debug build)", "Godot editor (Debug build)"], profiles.Select(pair => pair.Key));
        foreach ((string _, JsonNode? profile) in profiles)
        {
            Assert.Equal("Executable", (string?)profile?["commandName"]);
            Assert.Equal("%GODOT4%", (string?)profile?["executablePath"]);
            Assert.Contains("--path .", (string?)profile?["commandLineArgs"], StringComparison.Ordinal);
            Assert.Contains("stale", (string?)profile?["netprintsNote"], StringComparison.Ordinal);
        }

        Assert.Contains("--editor", (string?)profiles["Godot editor (Debug build)"]?["commandLineArgs"], StringComparison.Ordinal);
        Assert.Contains(template.Files ?? [], file => file.Path == "project.godot");
    }

    [Fact]
    public async Task TheCommandTheStatusBarItemAndTheSettingsPageAreContributed()
    {
        await using ExtensionHarness harness = await LoadAsync();

        CommandDescriptor command = Assert.Single(Recorded<CommandDescriptor>(harness));
        StatusBarItemDescriptor status = Assert.Single(Recorded<StatusBarItemDescriptor>(harness));
        SettingsPageDescriptor page = Assert.Single(Recorded<SettingsPageDescriptor>(harness));

        Assert.Equal(GodotCommandHandler.CommandId, command.Id);
        Assert.StartsWith(GodotExtension.Id + ".", status.Id, StringComparison.Ordinal);
        Assert.Equal(GodotExtension.Id, page.SectionKey);
        Assert.Null(page.CreateViewModel);
        Assert.Contains(harness.Registry.Settings, section => section.ExtensionId == GodotExtension.Id);
    }

    [Fact]
    public void TheSettingsDefaultHasNoExecutableAndPassesItsOwnValidation()
    {
        GodotSettings settings = GodotSettings.Descriptor.Default;

        Assert.Equal(string.Empty, settings.ExecutablePath);
        Assert.Empty(GodotSettings.Descriptor.Validator?.Invoke(settings) ?? []);
        Assert.NotEmpty(GodotSettings.Descriptor.Validator?.Invoke(new GodotSettings("relative/godot")) ?? []);
    }

    [Theory]
    [InlineData(null, "", "Godot: set GODOT4 or the executable path in the settings")]
    [InlineData("/opt/godot", "", "Godot: GODOT4 is set")]
    [InlineData(null, "/opt/godot", "Godot: /opt/godot")]
    [InlineData("/opt/godot", "/usr/bin/godot", "Godot: /usr/bin/godot")]
    public void TheStatusTextSaysWhichExecutableIsInUse(string? variable, string configured, string expected)
    {
        Assert.Equal(expected, GodotStatus.Text(variable, new GodotSettings(configured)));
    }
}
