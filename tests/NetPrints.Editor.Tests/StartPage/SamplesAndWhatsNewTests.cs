using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.Dialogs;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.State;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;
using NetPrints.Editor.Tests.State;

namespace NetPrints.Editor.Tests.StartPage;

/// <summary>The samples tile and the open-a-sample flow (FR-043), and the what's new tile (FR-044).</summary>
public sealed class SamplesAndWhatsNewTests : IDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private sealed class RecordingLauncher : IUrlLauncher
    {
        public List<string> Opened { get; } = [];

        public void Open(string url) => Opened.Add(url);
    }

    private readonly TestEditor editor = TestEditor.Create(TestEditor.CreateReflectionHost);
    private readonly FakeProjectActions actions = new();
    private readonly List<string> cleanup = [];
    private readonly List<ProjectRig> rigs = [];

    public void Dispose()
    {
        rigs.ForEach(rig => rig.Dispose());
        cleanup.ForEach(TestPaths.TryDelete);
    }

    private string TempRoot()
    {
        string root = TestPaths.CreateTempDirectory();
        cleanup.Add(root);
        return root;
    }

    private static string BundledHash()
    {
        SampleDescriptor sample = SampleCatalog.Bundled.Samples.Single(candidate => candidate.Name == "HelloWorld");
        return Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(Path.Combine(sample.Directory, "HelloWorld.Program.netpc.json"))));
    }

    [Fact]
    public async Task TheSamplesTileListsTheBundledSamplesAndOpensOneAfterTheUnloadPrompt()
    {
        var tile = new SamplesTileViewModel(SampleCatalog.Bundled, actions);
        SampleDescriptor hello = Assert.Single(tile.Samples, sample => sample.Name == "HelloWorld");

        await tile.OpenCommand.ExecuteAsync(hello);

        Assert.Equal(["ConfirmUnload", "OpenSample:HelloWorld"], actions.Calls);
    }

    [Fact]
    public async Task DecliningTheUnloadPromptDoesNotOpenTheSample()
    {
        actions.AllowUnload = false;
        var tile = new SamplesTileViewModel(SampleCatalog.Bundled, actions);

        await tile.OpenCommand.ExecuteAsync(tile.Samples[0]);

        Assert.Equal(["ConfirmUnload"], actions.Calls);
    }

    private ProjectRig NewRig()
    {
        var rig = new ProjectRig(editor.Context);
        rigs.Add(rig);
        return rig;
    }

    private string DefaultTarget(string name) => Path.Combine(editor.Locations.DefaultLocation, name);

    [Fact]
    public async Task OpeningASampleAsksOnceNamingTheDefaultTargetThenCopiesAndOpensTheCopy()
    {
        ProjectRig rig = NewRig();
        string before = BundledHash();

        await rig.Actions.OpenSampleAsync("HelloWorld", Token);

        Assert.Equal([("HelloWorld", DefaultTarget("HelloWorld"))], editor.Dialogs.SampleTargetCalls);
        string copy = Path.Combine(DefaultTarget("HelloWorld"), "HelloWorld.csproj");
        Assert.True(File.Exists(copy));
        Assert.Equal(copy, rig.Project?.Path);
        Assert.Equal(before, BundledHash());
        Assert.Empty(editor.Dialogs.Errors);
        Assert.Empty(editor.FilePicker.Calls);
    }

    [Fact]
    public async Task ASampleFolderThatExistsGetsANumericSuffixAndKeepsWhatIsThere()
    {
        ProjectRig rig = NewRig();
        string taken = DefaultTarget("HelloWorld");
        Directory.CreateDirectory(taken);
        await File.WriteAllTextAsync(Path.Combine(taken, "mine.txt"), "mine", Token);
        Directory.CreateDirectory(DefaultTarget("HelloWorld2"));

        await rig.Actions.OpenSampleAsync("HelloWorld", Token);

        Assert.Equal(DefaultTarget("HelloWorld3"), Assert.Single(editor.Dialogs.SampleTargetCalls).Target);
        Assert.Equal(Path.Combine(DefaultTarget("HelloWorld3"), "HelloWorld.csproj"), rig.Project?.Path);
        Assert.Equal(["mine.txt"], Directory.GetFileSystemEntries(taken).Select(Path.GetFileName));
    }

    [Fact]
    public async Task ChangeAsksForAFolderThenConfirmsTheNewTargetAndRemembersIt()
    {
        string documents = TempRoot();
        string picked = TempRoot();
        JsonEditorStateStore store = new(new EditorDataPaths("/state-root"), new InMemoryEditorFileSystem(), NullLogger<JsonEditorStateStore>.Instance);
        ProjectRig rig = new(editor.Context with { StateStore = store, Locations = new ProjectLocations(store, documents) });
        rigs.Add(rig);
        editor.Dialogs.SampleTargetAnswers.Enqueue(SampleTargetChoice.Change);
        editor.FilePicker.FolderAnswers.Enqueue(picked);

        await rig.Actions.OpenSampleAsync("HelloWorld", Token);

        Assert.Equal([Path.Combine(documents, "NetPrints", "HelloWorld"), Path.Combine(picked, "HelloWorld")], editor.Dialogs.SampleTargetCalls.Select(call => call.Target));
        Assert.Equal(Path.Combine(picked, "HelloWorld", "HelloWorld.csproj"), rig.Project?.Path);
        Assert.Equal(picked, new ProjectLocations(store, documents).Last);
    }

    [Fact]
    public async Task ChangeThenCancellingThePickerAsksAgainAboutTheSameTarget()
    {
        ProjectRig rig = NewRig();
        editor.Dialogs.SampleTargetAnswers.Enqueue(SampleTargetChoice.Change);
        editor.Dialogs.SampleTargetAnswers.Enqueue(SampleTargetChoice.Cancel);
        editor.FilePicker.FolderAnswers.Enqueue(null);

        await rig.Actions.OpenSampleAsync("HelloWorld", Token);

        Assert.Equal([DefaultTarget("HelloWorld"), DefaultTarget("HelloWorld")], editor.Dialogs.SampleTargetCalls.Select(call => call.Target));
        Assert.Null(rig.Session);
        Assert.False(Directory.Exists(DefaultTarget("HelloWorld")));
    }

    [Fact]
    public async Task CancellingTheConfirmationOpensNothingAndWritesNothing()
    {
        ProjectRig rig = NewRig();
        editor.Dialogs.SampleTargetAnswers.Enqueue(SampleTargetChoice.Cancel);

        await rig.Actions.OpenSampleAsync("HelloWorld", Token);

        Assert.Null(rig.Session);
        Assert.Empty(editor.Dialogs.Errors);
        Assert.False(Directory.Exists(DefaultTarget("HelloWorld")));
    }

    [Fact]
    public async Task AnUnknownSampleShowsAnError()
    {
        ProjectRig rig = NewRig();

        await rig.Actions.OpenSampleAsync("NoSuchSample", Token);

        Assert.Equal("Failed to open the sample", Assert.Single(editor.Dialogs.Errors).Title);
        Assert.Empty(editor.FilePicker.Calls);
        Assert.Empty(editor.Dialogs.SampleTargetCalls);
    }

    [Fact]
    public void TheWhatsNewTileShowsTheNotesAsBlocksAndOpensItsLinks()
    {
        var launcher = new RecordingLauncher();
        var tile = new WhatsNewTileViewModel("# Title\n- a [link](https://example.com/a)", launcher);

        Assert.Equal(2, tile.Blocks.Count);
        Assert.IsType<WhatsNewHeading>(tile.Blocks[0]);

        tile.OpenLinkCommand.Execute("https://example.com/a");

        Assert.Equal(["https://example.com/a"], launcher.Opened);
    }

    [Fact]
    public void TheBundledNotesDoNotRepeatTheReleaseNotesLinkTheLearnCardHas()
    {
        string notes = WhatsNewResource.Read();

        Assert.DoesNotContain("/releases", notes, StringComparison.Ordinal);
        Assert.DoesNotContain("## More", notes, StringComparison.Ordinal);
    }

    [Fact]
    public void TheDefaultLauncherIgnoresAddressesThatAreNotWebPages()
    {
        var launcher = new ShellUrlLauncher();

        launcher.Open("file:///etc/passwd");
        launcher.Open("javascript:void");
        launcher.Open("not a url");
    }
}
