namespace NetPrints.Editor.UITests.Hosting;

/// <summary>Artifact names come from test display names, which can hold characters actions/upload-artifact rejects.</summary>
public class UiArtifactsTests
{
    private static readonly char[] Rejected = ['"', ':', '<', '>', '|', '*', '?', '\r', '\n', '\\', '/'];

    [Fact]
    public void ASafeNameHoldsNoCharacterTheArtifactUploadRejects()
    {
        string name = UiArtifacts.SafeName("Tests.Open(path: \"a:b\\c/d\", mode: <x>|y*z?)\r\nnext");

        Assert.DoesNotContain(name, character => Rejected.Contains(character));
        Assert.DoesNotContain(' ', name);
        Assert.NotEmpty(name);
    }

    [Fact]
    public void AnOrdinaryNameIsKeptReadable() =>
        Assert.Equal("Tests.Open_the_project", UiArtifacts.SafeName("Tests.Open the project"));

    [Fact]
    public void ABlankNameFallsBack() => Assert.Equal("unknown", UiArtifacts.SafeName("  "));
}
