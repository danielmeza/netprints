using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using NetPrints.Cli.Tests.Support;
using Xunit;

namespace NetPrints.Cli.Tests.Commands;

/// <summary>GI-T09: <c>git-install</c> configures the diff text conversion and the merge driver in temporary repositories.</summary>
public sealed class GitInstallCommandTests : IAsyncLifetime
{
    private const string DiffLine = "*.netpc.json diff=netprints";
    private const string MergeLine = "*.netpc.json diff=netprints merge=netprints";

    private TempGitRepository? _created;
    private string? _other;

    private TempGitRepository Repo => _created ?? throw new InvalidOperationException("The repository is created by InitializeAsync.");

    public async ValueTask InitializeAsync() => _created = await TempGitRepository.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _created?.Dispose();
        if (_other is not null)
        {
            Directory.Delete(_other, recursive: true);
        }

        return ValueTask.CompletedTask;
    }

    private async Task<(int Exit, CliTestHost Host)> InstallAsync(params string[] args)
    {
        var host = new CliTestHost(Repo.Path);
        int exit = await host.RunRealAsync(["git-install", .. args]);
        return (exit, host);
    }

    [Fact]
    public async Task InstallSetsTheTextconvAndWritesTheAttributesLine()
    {
        (int exit, CliTestHost host) = await InstallAsync();

        host.AssertExit(ExitCodes.Success, exit);
        Assert.Equal("netprints show", await Repo.ConfigAsync("diff.netprints.textconv"));
        Assert.Null(await Repo.ConfigAsync("merge.netprints.driver"));
        Assert.Equal(DiffLine + "\n", File.ReadAllText(Repo.File(".gitattributes")));
        Assert.Contains("installed", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnAttributesFileThatCannotBeWrittenExitsOneInsteadOfAnInternalError()
    {
        Directory.CreateDirectory(Repo.File(".gitattributes"));

        (int exit, CliTestHost host) = await InstallAsync();

        host.AssertExit(ExitCodes.Failed, exit);
        Assert.DoesNotContain("Internal error", host.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ASecondRunReportsAlreadyInstalledAndWritesNothing()
    {
        await InstallAsync();
        string attributes = Repo.File(".gitattributes");
        byte[] before = File.ReadAllBytes(attributes);
        DateTime modified = File.GetLastWriteTimeUtc(attributes);

        (int exit, CliTestHost host) = await InstallAsync();

        host.AssertExit(ExitCodes.Success, exit);
        Assert.Contains("already installed", host.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("installed:", host.Output.Replace("already installed", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.Equal(before, File.ReadAllBytes(attributes));
        Assert.Equal(modified, File.GetLastWriteTimeUtc(attributes));
    }

    [Fact]
    public async Task MergeAddsTheDriverAndUpgradesTheLineInPlace()
    {
        await InstallAsync();

        (int exit, CliTestHost host) = await InstallAsync("--merge");

        host.AssertExit(ExitCodes.Success, exit);
        Assert.Equal("NetPrints graph merge", await Repo.ConfigAsync("merge.netprints.name"));
        Assert.Equal("netprints merge %O %A %B --marker-size %L --path %P", await Repo.ConfigAsync("merge.netprints.driver"));
        Assert.Equal(MergeLine + "\n", File.ReadAllText(Repo.File(".gitattributes")));
    }

    [Fact]
    public async Task CommandOptionIsUsedInTheConfigValues()
    {
        (int exit, CliTestHost host) = await InstallAsync("--merge", "--command", "dotnet /opt/np/NetPrints.Cli.dll");

        host.AssertExit(ExitCodes.Success, exit);
        Assert.Equal("dotnet /opt/np/NetPrints.Cli.dll show", await Repo.ConfigAsync("diff.netprints.textconv"));
        Assert.Equal("dotnet /opt/np/NetPrints.Cli.dll merge %O %A %B --marker-size %L --path %P", await Repo.ConfigAsync("merge.netprints.driver"));
    }

    [Fact]
    public async Task InstallKeepsTheOtherLinesAndTheLineEndingsOfTheFile()
    {
        File.WriteAllBytes(Repo.File(".gitattributes"), Encoding.ASCII.GetBytes("*.png binary\r\n*.sh text eol=lf"));

        (int exit, CliTestHost host) = await InstallAsync();

        host.AssertExit(ExitCodes.Success, exit);
        Assert.Equal("*.png binary\r\n*.sh text eol=lf\r\n" + DiffLine + "\r\n", File.ReadAllText(Repo.File(".gitattributes")));
    }

    [Fact]
    public async Task UninstallRemovesExactlyWhatInstallAdded()
    {
        File.WriteAllText(Repo.File(".gitattributes"), "*.png binary\n");
        await InstallAsync("--merge");

        (int exit, CliTestHost host) = await InstallAsync("--uninstall");

        host.AssertExit(ExitCodes.Success, exit);
        Assert.Null(await Repo.ConfigAsync("diff.netprints.textconv"));
        Assert.Null(await Repo.ConfigAsync("merge.netprints.driver"));
        Assert.Null(await Repo.ConfigAsync("merge.netprints.name"));
        Assert.Equal("*.png binary\n", File.ReadAllText(Repo.File(".gitattributes")));
        Assert.Contains("removed", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UninstallDeletesAnAttributesFileThatHeldOnlyOurLine()
    {
        await InstallAsync();

        (int exit, _) = await InstallAsync("--uninstall");

        Assert.Equal(ExitCodes.Success, exit);
        Assert.False(File.Exists(Repo.File(".gitattributes")));
    }

    [Fact]
    public async Task UninstallOnAFreshRepositoryChangesNothingAndSucceeds()
    {
        (int exit, CliTestHost host) = await InstallAsync("--uninstall");

        host.AssertExit(ExitCodes.Success, exit);
        Assert.Contains("not installed", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AConflictingDriverLineIsKeptAndNothingIsWritten()
    {
        const string Existing = "*.netpc.json diff=other\n";
        File.WriteAllText(Repo.File(".gitattributes"), Existing);

        (int exit, CliTestHost host) = await InstallAsync("--merge");

        host.AssertExit(ExitCodes.Failed, exit);
        Assert.Equal(Existing, File.ReadAllText(Repo.File(".gitattributes")));
        Assert.Null(await Repo.ConfigAsync("diff.netprints.textconv"));
        Assert.Null(await Repo.ConfigAsync("merge.netprints.driver"));
        Assert.Contains("diff=other", host.Output + host.Error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OutsideAWorkTreeExitsTwo()
    {
        string plain = Directory.CreateTempSubdirectory("np-nogit-").FullName;
        _other = plain;
        var host = new CliTestHost(plain);

        int exit = await host.RunRealAsync("git-install");

        host.AssertExit(ExitCodes.Usage, exit);
        Assert.Contains("not inside a git work tree", host.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task GlobalWritesTheUserConfigAndTheDefaultAttributesFileAndUninstallRemovesThem()
    {
        string home = Directory.CreateTempSubdirectory("np-home-").FullName;
        _other = home;
        string gitConfig = Path.Combine(home, "gitconfig");
        var variables = new Dictionary<string, string?>
        {
            ["HOME"] = home,
            ["XDG_CONFIG_HOME"] = Path.Combine(home, "xdg"),
            ["GIT_CONFIG_GLOBAL"] = gitConfig,
        };

        var install = new CliTestHost(home, variables);
        install.AssertExit(ExitCodes.Success, await install.RunRealAsync("git-install", "--global", "--merge"));

        string config = File.ReadAllText(gitConfig);
        Assert.Contains("textconv = netprints show", config, StringComparison.Ordinal);
        Assert.Contains("driver = netprints merge %O %A %B --marker-size %L --path %P", config, StringComparison.Ordinal);
        string attributes = Path.Combine(home, "xdg", "git", "attributes");
        Assert.Equal(MergeLine + "\n", File.ReadAllText(attributes));

        var uninstall = new CliTestHost(home, variables);
        uninstall.AssertExit(ExitCodes.Success, await uninstall.RunRealAsync("git-install", "--global", "--uninstall"));

        string after = File.ReadAllText(gitConfig);
        Assert.DoesNotContain("textconv", after, StringComparison.Ordinal);
        Assert.DoesNotContain("driver", after, StringComparison.Ordinal);
        Assert.False(File.Exists(attributes));
    }
}
