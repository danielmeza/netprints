using System.Security.Cryptography;
using System.Text;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Tests.State;

public sealed class EditorDataPathsTests
{
    [Fact]
    public void TheRootIsTheNetPrintsFolderOfTheApplicationData()
    {
        EditorDataPaths paths = EditorDataPaths.Resolve(_ => null, Path.Combine("data", "app"));

        Assert.Equal(Path.Combine("data", "app", "NetPrints"), paths.Root);
        Assert.Equal(Path.Combine("data", "app", "NetPrints", "state"), paths.StateDirectory);
        Assert.Equal(Path.Combine("data", "app", "NetPrints", "state", "sessions"), paths.SessionsDirectory);
        Assert.Equal(Path.Combine("data", "app", "NetPrints", "backups"), paths.BackupsDirectory);
        Assert.Equal(Path.Combine("data", "app", "NetPrints", "backups", "0123456789abcdef"), paths.BackupDirectoryOf("0123456789abcdef"));
    }

    [Fact]
    public void TheStateDirVariableReplacesTheRoot()
    {
        EditorDataPaths paths = EditorDataPaths.Resolve(name => name == "NETPRINTS_STATE_DIR" ? "override" : null, "app");

        Assert.Equal("override", paths.Root);
        Assert.Equal(Path.Combine("override", "backups"), paths.BackupsDirectory);
    }

    [Fact]
    public void AnEmptyStateDirVariableIsIgnored()
    {
        EditorDataPaths paths = EditorDataPaths.Resolve(_ => "", "app");

        Assert.Equal(Path.Combine("app", "NetPrints"), paths.Root);
    }

    [Fact]
    public void TheProjectKeyIsTheFirst16HexCharactersOfTheSha256OfTheFullPath()
    {
        string path = Path.GetFullPath(Path.Combine("projects", "Hello", "Hello.csproj"));
        string expected = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(path)))[..16];

        Assert.Equal(expected, EditorDataPaths.ProjectKey(path));
        Assert.Equal(expected, EditorDataPaths.ProjectKey(Path.Combine("projects", "Hello", "..", "Hello", "Hello.csproj")));
        Assert.Equal(16, expected.Length);
        Assert.Matches("^[0-9a-f]{16}$", expected);
    }

    [Fact]
    public void WindowsCaseFoldsThePathBeforeHashing()
    {
        string path = Path.GetFullPath(Path.Combine("projects", "Hello", "Hello.csproj"));

        Assert.Equal(EditorDataPaths.ProjectKey(path, caseFold: true), EditorDataPaths.ProjectKey(path.ToUpperInvariant(), caseFold: true));
        Assert.NotEqual(EditorDataPaths.ProjectKey(path, caseFold: false), EditorDataPaths.ProjectKey(path.ToUpperInvariant(), caseFold: false));
    }
}
