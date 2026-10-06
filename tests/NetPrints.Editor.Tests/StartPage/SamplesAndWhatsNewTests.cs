using System.Security.Cryptography;
using NetPrints.Editor.Hosting;
using NetPrints.Editor.StartPage;
using NetPrints.Editor.Tests.Hosting;
using NetPrints.Editor.Tests.Shell;

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

    [Fact]
    public async Task OpeningASampleCopiesItToTheChosenFolderAndOpensTheCopy()
    {
        ProjectRig rig = NewRig();
        string before = BundledHash();
        string parent = TempRoot();
        editor.FilePicker.FolderAnswers.Enqueue(parent);

        await rig.Actions.OpenSampleAsync("HelloWorld", Token);

        string copy = Path.Combine(parent, "HelloWorld", "HelloWorld.csproj");
        Assert.True(File.Exists(copy));
        Assert.Equal(copy, rig.Project?.Path);
        Assert.Equal(before, BundledHash());
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact]
    public async Task CancellingTheFolderPickerOpensNothingAndWritesNothing()
    {
        ProjectRig rig = NewRig();
        editor.FilePicker.FolderAnswers.Enqueue(null);

        await rig.Actions.OpenSampleAsync("HelloWorld", Token);

        Assert.Null(rig.Session);
        Assert.Empty(editor.Dialogs.Errors);
    }

    [Fact]
    public async Task ACopyIntoAFolderThatIsNotEmptyShowsAnErrorAndKeepsWhatIsThere()
    {
        ProjectRig rig = NewRig();
        string parent = TempRoot();
        string target = Path.Combine(parent, "HelloWorld");
        Directory.CreateDirectory(target);
        await File.WriteAllTextAsync(Path.Combine(target, "mine.txt"), "mine", Token);
        editor.FilePicker.FolderAnswers.Enqueue(parent);

        await rig.Actions.OpenSampleAsync("HelloWorld", Token);

        Assert.Null(rig.Session);
        Assert.Equal("Failed to open the sample", Assert.Single(editor.Dialogs.Errors).Title);
        Assert.Equal(["mine.txt"], Directory.GetFileSystemEntries(target).Select(Path.GetFileName));
    }

    [Fact]
    public async Task AnUnknownSampleShowsAnError()
    {
        ProjectRig rig = NewRig();

        await rig.Actions.OpenSampleAsync("NoSuchSample", Token);

        Assert.Equal("Failed to open the sample", Assert.Single(editor.Dialogs.Errors).Title);
        Assert.Empty(editor.FilePicker.Calls);
    }

    [Fact]
    public void TheWhatsNewTileShowsTheNotesAsBlocksAndOpensItsLinks()
    {
        var launcher = new RecordingLauncher();
        var tile = new WhatsNewTileViewModel("# Title\n- a [link](https://example.com/a)", launcher);

        Assert.Equal(2, tile.Blocks.Count);
        Assert.IsType<WhatsNewHeading>(tile.Blocks[0]);

        tile.OpenLinkCommand.Execute("https://example.com/a");
        tile.OpenLinkCommand.Execute(WhatsNewTileViewModel.ReleasesUrl);

        Assert.Equal(["https://example.com/a", "https://github.com/danielmeza/netprints/releases"], launcher.Opened);
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
