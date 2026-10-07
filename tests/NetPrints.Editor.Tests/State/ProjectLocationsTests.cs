using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Tests.State;

/// <summary>Where new projects and copied samples go by default and the last place the user used (FR-048).</summary>
public sealed class ProjectLocationsTests
{
    private readonly InMemoryEditorFileSystem fileSystem = new();

    private JsonEditorStateStore Store() => new(new EditorDataPaths("/state-root"), fileSystem, NullLogger<JsonEditorStateStore>.Instance);

    [Fact]
    public void TheDefaultIsTheNetPrintsFolderUnderTheDocumentsFolder()
    {
        var locations = new ProjectLocations(Store(), "/docs");

        Assert.Equal(Path.Combine("/docs", "NetPrints"), locations.DefaultLocation);
        Assert.Equal(locations.DefaultLocation, locations.Last);
    }

    [Fact]
    public void ARememberedLocationWinsAndSurvivesANewInstance()
    {
        new ProjectLocations(Store(), "/docs").Remember("/work/projects");

        Assert.Equal("/work/projects", new ProjectLocations(Store(), "/docs").Last);
    }

    [Fact]
    public void RememberingKeepsTheOtherStartState()
    {
        JsonEditorStateStore store = Store();
        store.SaveStart(new StartState(StateFile.CurrentVersion, "0.2.0"));

        new ProjectLocations(store, "/docs").Remember("/work");

        Assert.Equal(new StartState(StateFile.CurrentVersion, "0.2.0", "/work"), store.LoadStart());
    }

    [Fact]
    public void WithoutAStoreTheDefaultIsAlwaysUsed()
    {
        var locations = new ProjectLocations(null, "/docs");

        locations.Remember("/work");

        Assert.Equal(locations.DefaultLocation, locations.Last);
    }

    [Fact]
    public void ABlankRememberedLocationFallsBackToTheDefault()
    {
        JsonEditorStateStore store = Store();
        store.SaveStart(new StartState(StateFile.CurrentVersion, null, ""));

        Assert.Equal(Path.Combine("/docs", "NetPrints"), new ProjectLocations(store, "/docs").Last);
    }

    [Fact]
    public void TheDefaultIsTheDocumentsFolderOrTheHomeFolderWhenThereIsNone()
    {
        ProjectLocations normal = ProjectLocations.Resolve(null, "/docs", "/user-home");
        ProjectLocations noDocuments = ProjectLocations.Resolve(null, "", "/user-home");

        Assert.Equal(Path.Combine("/docs", "NetPrints"), normal.DefaultLocation);
        Assert.Equal(Path.Combine("/user-home", "NetPrints"), noDocuments.DefaultLocation);
    }
}
