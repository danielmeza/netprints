using NetPrints.Editor.Hosting;

namespace NetPrints.Editor.Tests.Hosting;

/// <summary>R7-1: extension folders and files are compared the way the file system does.</summary>
public sealed class ExtensionPathsTests
{
    [Fact]
    public void ATrailingSeparatorNamesTheSameFolder()
    {
        string folder = Path.Combine("extensions", "chart-tools");

        Assert.True(ExtensionPaths.Same(folder, folder + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void TwoDifferentFoldersDiffer()
    {
        Assert.False(ExtensionPaths.Same(Path.Combine("extensions", "chart-tools"), Path.Combine("extensions", "other-tools")));
    }

    [Fact]
    public void LetterCaseMattersOnlyOnWindows()
    {
        string lower = Path.Combine("extensions", "chart-tools");
        string upper = Path.Combine("extensions", "CHART-TOOLS");

        Assert.Equal(OperatingSystem.IsWindows(), ExtensionPaths.Same(lower, upper));
    }
}
