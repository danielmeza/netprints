using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using NetPrints.Cli.Git;
using NetPrints.Cli.Tests.Support;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Git;

public sealed class MergeCommandFallbackTests : IDisposable
{
    private static readonly string FixtureRoot = Path.Combine(LocalSdkLayout.FindRepositoryRoot(), "tests", "NetPrints.Cli.Tests", "Git", "Fixtures", "PinConflict");

    private readonly string _directory = Directory.CreateTempSubdirectory("netprints-merge-fallback-").FullName;

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task AMergerThatThrowsFallsBackToAMarkedTextMergeAndExitsOne()
    {
        foreach (string name in new[] { "base", "ours", "theirs" })
        {
            File.Copy(Path.Combine(FixtureRoot, name + ".netpc.json"), Path.Combine(_directory, name + ".netpc.json"));
        }

        var host = new CliTestHost(_directory);
        IServiceCollection services = CliServices.CreateDefault();
        services.AddSingleton(host.Environment);
        services.AddSingleton(host.Console);
        GraphMergerFactory failing = _ => throw new InvalidOperationException("boom");
        services.AddSingleton(failing);

        int exitCode = await CliApplication.RunAsync(["merge", "base.netpc.json", "ours.netpc.json", "theirs.netpc.json"], services, TestContext.Current.CancellationToken);

        host.AssertExit(ExitCodes.Failed, exitCode);
        Assert.Contains("merge failed: boom; merging as text", host.Error.ToString(), StringComparison.Ordinal);
        Assert.Contains("<<<<<<<", File.ReadAllText(Path.Combine(_directory, "ours.netpc.json")), StringComparison.Ordinal);
    }
}
