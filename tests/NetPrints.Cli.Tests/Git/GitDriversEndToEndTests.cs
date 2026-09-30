using System;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Cli.Tests.Support;
using NetPrints.Projects;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Git;

/// <summary>GI-T10: real <c>git</c> diffs through <c>show</c> and merges through the driver installed by <c>git-install</c> (SC-009, SC-010).</summary>
public sealed class GitDriversEndToEndTests : IAsyncLifetime
{
    private const string GraphFile = "Program.netpc.json";

    private static readonly string FixtureRoot = Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "tests", "NetPrints.Cli.Tests", "Git", "Fixtures", "MergeClean");

    private TempGitRepository? _created;

    private TempGitRepository Repo => _created ?? throw new InvalidOperationException("The repository is created by InitializeAsync.");

    public async ValueTask InitializeAsync() => _created = await TempGitRepository.CreateAsync();

    public ValueTask DisposeAsync()
    {
        _created?.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string CliUnderTest => "dotnet " + Path.Combine(AppContext.BaseDirectory, "NetPrints.Cli.dll");

    private async Task InstallAsync()
    {
        var host = new CliTestHost(Repo.Path);
        host.AssertExit(ExitCodes.Success, await host.RunRealAsync("git-install", "--merge", "--command", CliUnderTest));
        await Repo.GitAsync("add", ".gitattributes");
        await Repo.GitAsync("commit", "-q", "-m", "attributes");
    }

    private async Task CommitAsync(string fixture, string message)
    {
        File.Copy(Path.Combine(FixtureRoot, fixture + ".netpc.json"), Repo.File(GraphFile), overwrite: true);
        await Repo.GitAsync("add", GraphFile);
        await Repo.GitAsync("commit", "-q", "-m", message);
    }

    /// <summary>Builds the GI-T03 history: a base commit, then <c>ours</c> on main and <c>theirs</c> on a side branch.</summary>
    private async Task BranchAsync()
    {
        await CommitAsync("base", "base");
        await Repo.GitAsync("checkout", "-q", "-b", "side");
        await CommitAsync("theirs", "theirs");
        await Repo.GitAsync("checkout", "-q", "main");
        await CommitAsync("ours", "ours");
    }

    [Fact]
    public async Task GitDiffShowsTheSummaryLinesOfTheChangedGraph()
    {
        await InstallAsync();
        await CommitAsync("base", "base");
        await CommitAsync("ours", "ours");

        string diff = await Repo.GitAsync("diff", "HEAD~1", "HEAD", "--", GraphFile);

        Assert.Contains("+    node n0000000000011 callMethod System.Console.WriteLine(System.String)", diff, StringComparison.Ordinal);
        Assert.DoesNotContain("$kind", diff, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GitMergeOfTwoBranchesEditingOneMethodSucceedsThroughTheDriver()
    {
        await InstallAsync();
        await BranchAsync();

        ProcessResult merge = await Repo.RunGitAsync("merge", "--no-edit", "side");

        Assert.True(merge.ExitCode == 0, merge.StandardOutput + merge.StandardError);
        Assert.Equal(File.ReadAllText(Path.Combine(FixtureRoot, "expected.netpc.json")), File.ReadAllText(Repo.File(GraphFile)));
        Assert.Equal(string.Empty, (await Repo.GitAsync("ls-files", "--unmerged")).Trim());
    }

    [Fact]
    public async Task PlainGitConflictsOnTheSameBranches()
    {
        await BranchAsync();

        ProcessResult merge = await Repo.RunGitAsync("merge", "--no-edit", "side");

        Assert.NotEqual(0, merge.ExitCode);
        Assert.Contains("CONFLICT", merge.StandardOutput, StringComparison.Ordinal);
        Assert.Contains("<<<<<<<", File.ReadAllText(Repo.File(GraphFile)), StringComparison.Ordinal);
    }
}
