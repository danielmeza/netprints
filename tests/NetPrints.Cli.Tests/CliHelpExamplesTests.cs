using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Cli.Tests.Support;
using NetPrints.Testing;
using Xunit;

namespace NetPrints.Cli.Tests;

/// <summary>The help a user copies from must work: example paths exist, and the help mentions <c>-- &lt;args&gt;</c> for <c>run</c> and the <c>regen</c> alias.</summary>
public sealed class CliHelpExamplesTests
{
    [Theory]
    [InlineData("--help")]
    [InlineData("run --help")]
    [InlineData("generate --help")]
    [InlineData("migrate --help")]
    [InlineData("format --help")]
    [InlineData("show --help")]
    public async Task EveryPathInAnExampleExists(string command)
    {
        var host = new CliTestHost();
        Assert.Equal(0, await host.RunAsync(command.Split(' ')));

        string root = LocalSdkLayout.FindRepositoryRoot();
        string[] paths = [.. host.Output.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("netprints ", StringComparison.Ordinal))
            .SelectMany(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(token => token.Contains('/', StringComparison.Ordinal))];

        Assert.NotEmpty(paths);
        foreach (string path in paths)
        {
            Assert.True(File.Exists(Path.Combine(root, path)) || Directory.Exists(Path.Combine(root, path)), $"example path '{path}' does not exist");
        }
    }

    [Fact]
    public async Task RunHelpShowsTheForwardedArgumentsSeparator()
    {
        var host = new CliTestHost();
        await host.RunAsync("run", "--help");

        Assert.Contains("-- arg1 arg2", host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheOverviewMentionsTheRegenAliasAndTheArgumentsSeparator()
    {
        var host = new CliTestHost();
        await host.RunAsync("--help");

        Assert.Contains("regen", host.Output, StringComparison.Ordinal);
        Assert.Contains("--", host.Output, StringComparison.Ordinal);
        Assert.Contains("go to the program", host.Output, StringComparison.Ordinal);
    }
}
