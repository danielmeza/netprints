using System.Security.Cryptography;
using System.Text;
using NetPrints.Editor.State;

namespace NetPrints.Editor.Tests.State;

public sealed class EditorDataPathsTests
{
    private static readonly string Home = Path.Combine("users", "alice");
    private static readonly UserFolders Folders = new(Home, Path.Combine(Home, "roaming"), Path.Combine(Home, "local"));

    private static Func<string, string?> Env(params (string Name, string Value)[] variables) =>
        name => variables.Where(v => v.Name == name).Select(v => v.Value).FirstOrDefault();

    [Fact]
    public void WindowsKeepsStateAndLogsInTheLocalFolderAndSettingsInTheRoamingOne()
    {
        EditorDataPaths paths = EditorDataPaths.Resolve(Env(), EditorPlatform.Windows, Folders);

        Assert.Equal(Path.Combine(Folders.LocalApplicationData, "NetPrints"), paths.Root);
        Assert.Equal(Path.Combine(Folders.LocalApplicationData, "NetPrints", "logs"), paths.LogDirectory);
        Assert.Equal(Path.Combine(Folders.ApplicationData, "NetPrints"), paths.ConfigDirectory);
        Assert.Equal(Path.Combine(Folders.ApplicationData, "NetPrints"), paths.LegacyRoot);
        Assert.Equal(Path.Combine(paths.Root, "state", "sessions"), paths.SessionsDirectory);
        Assert.Equal(Path.Combine(paths.Root, "backups", "0123456789abcdef"), paths.BackupDirectoryOf("0123456789abcdef"));
    }

    [Fact]
    public void MacOsKeepsLogsInLibraryLogsAndStateInApplicationSupport()
    {
        EditorDataPaths paths = EditorDataPaths.Resolve(Env(), EditorPlatform.MacOS, Folders);

        Assert.Equal(Path.Combine(Home, "Library", "Application Support", "NetPrints"), paths.Root);
        Assert.Equal(Path.Combine(Home, "Library", "Logs", "NetPrints"), paths.LogDirectory);
        Assert.Equal(Path.Combine(Folders.ApplicationData, "NetPrints"), paths.ConfigDirectory);
    }

    [Fact]
    public void LinuxUsesXdgStateHomeWhenItIsAbsolute()
    {
        string xdg = Path.GetFullPath(Path.Combine("xdg", "state"));

        EditorDataPaths paths = EditorDataPaths.Resolve(Env(("XDG_STATE_HOME", xdg)), EditorPlatform.Linux, Folders);

        Assert.Equal(Path.Combine(xdg, "NetPrints"), paths.Root);
        Assert.Equal(Path.Combine(xdg, "NetPrints", "logs"), paths.LogDirectory);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/state")]
    public void LinuxFallsBackToTheLocalStateFolder(string? xdg)
    {
        EditorDataPaths paths = EditorDataPaths.Resolve(Env(("XDG_STATE_HOME", xdg ?? string.Empty)), EditorPlatform.Linux, Folders);

        Assert.Equal(Path.Combine(Home, ".local", "state", "NetPrints"), paths.Root);
        Assert.Equal(Path.Combine(Home, ".local", "state", "NetPrints", "logs"), paths.LogDirectory);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TheStateDirVariableReplacesTheRootAndPutsLogsUnderIt(int platformIndex)
    {
        EditorPlatform platform = new[] { EditorPlatform.Windows, EditorPlatform.MacOS, EditorPlatform.Linux }[platformIndex];

        EditorDataPaths paths = EditorDataPaths.Resolve(Env(("NETPRINTS_STATE_DIR", "override")), platform, Folders);

        Assert.Equal("override", paths.Root);
        Assert.Equal(Path.Combine("override", "logs"), paths.LogDirectory);
        Assert.Equal(Path.Combine("override", "backups"), paths.BackupsDirectory);
        Assert.Null(paths.LegacyRoot);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void TheLogDirVariableBeatsTheStateDirAndTheDefault(int platformIndex)
    {
        EditorPlatform platform = new[] { EditorPlatform.Windows, EditorPlatform.MacOS, EditorPlatform.Linux }[platformIndex];

        EditorDataPaths both = EditorDataPaths.Resolve(Env(("NETPRINTS_STATE_DIR", "state"), ("NETPRINTS_LOG_DIR", "logs-here")), platform, Folders);
        EditorDataPaths logOnly = EditorDataPaths.Resolve(Env(("NETPRINTS_LOG_DIR", "logs-here")), platform, Folders);

        Assert.Equal("logs-here", both.LogDirectory);
        Assert.Equal("state", both.Root);
        Assert.Equal("logs-here", logOnly.LogDirectory);
        Assert.NotNull(logOnly.LegacyRoot);
    }

    [Fact]
    public void EmptyVariablesAreIgnored()
    {
        EditorDataPaths paths = EditorDataPaths.Resolve(Env(("NETPRINTS_STATE_DIR", ""), ("NETPRINTS_LOG_DIR", "")), EditorPlatform.Windows, Folders);

        Assert.Equal(Path.Combine(Folders.LocalApplicationData, "NetPrints"), paths.Root);
        Assert.Equal(Path.Combine(Folders.LocalApplicationData, "NetPrints", "logs"), paths.LogDirectory);
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
