using System;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Cli.Infrastructure;
using NetPrints.Cli.Tests.Support;
using NetPrints.Extensibility.Loading;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Cli.Tests.Commands;

/// <summary>CL-T08: <c>generate</c> against a temp copy of HelloWorld and the real SDK.</summary>
public sealed class GenerateCommandTests : IDisposable
{
    private const string SecondGraph = "Second.Program.netpc.json";
    private const string SecondGenerated = "Second.Program.netpc.g.cs";

    private readonly SampleCopy _sample = new();
    private readonly CliTestHost _host;

    public GenerateCommandTests() => _host = new CliTestHost(_sample.Directory);

    public void Dispose() => _sample.Dispose();

    [Fact]
    public async Task AFreshProjectExitsZeroAndWritesNothing()
    {
        string generated = _sample.Combine(SampleCopy.GeneratedName);
        string before = File.ReadAllText(generated);

        int exitCode = await _host.RunRealAsync("generate");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal(before, File.ReadAllText(generated));
        Assert.Contains("1 generated file(s) up to date, 0 written.", _host.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("generated: ", _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OnlyTheStaleFileIsRewritten()
    {
        File.Copy(_sample.Combine(SampleCopy.GraphName), _sample.Combine(SecondGraph));
        Assert.Equal(ExitCodes.Success, await _host.RunRealAsync("generate"));
        string second = _sample.Combine(SecondGenerated);
        Assert.True(File.Exists(second));

        string first = _sample.Combine(SampleCopy.GeneratedName);
        File.WriteAllText(first, "// stale\n");
        string secondBefore = File.ReadAllText(second);
        var host = new CliTestHost(_sample.Directory);

        int exitCode = await host.RunRealAsync("generate");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("generated: " + SampleCopy.GeneratedName, host.Output, StringComparison.Ordinal);
        Assert.DoesNotContain(SecondGenerated, host.Output, StringComparison.Ordinal);
        Assert.Contains("1 generated file(s) up to date, 1 written.", host.Output, StringComparison.Ordinal);
        Assert.NotEqual("// stale\n", File.ReadAllText(first));
        Assert.Equal(secondBefore, File.ReadAllText(second));
    }

    [Fact]
    public async Task CheckNamesTheStaleFileExitsOneAndWritesNothing()
    {
        string generated = _sample.Combine(SampleCopy.GeneratedName);
        File.WriteAllText(generated, "// stale\n");

        int exitCode = await _host.RunRealAsync("generate", "--check");

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Contains("stale: " + SampleCopy.GeneratedName, _host.Output, StringComparison.Ordinal);
        Assert.Contains("1 stale.", _host.Output, StringComparison.Ordinal);
        Assert.Equal("// stale\n", File.ReadAllText(generated));
    }

    [Fact]
    public async Task CheckTreatsAMissingGeneratedFileAsStaleWithoutCreatingIt()
    {
        string generated = _sample.Combine(SampleCopy.GeneratedName);
        File.Delete(generated);

        int exitCode = await _host.RunRealAsync("regen", "--check");

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Contains("stale: " + SampleCopy.GeneratedName, _host.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(generated));
    }

    [Fact]
    public async Task CheckOfAFreshProjectExitsZero()
    {
        Assert.Equal(ExitCodes.Success, await _host.RunRealAsync("generate", "--check", _sample.Project));
        Assert.Contains("up to date", _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GraphOptionLimitsTheRunToThatGraph()
    {
        File.Copy(_sample.Combine(SampleCopy.GraphName), _sample.Combine(SecondGraph));
        File.WriteAllText(_sample.Combine(SampleCopy.GeneratedName), "// stale\n");

        int exitCode = await _host.RunRealAsync("generate", "--check", "--graph", SecondGraph);

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Contains("stale: " + SecondGenerated, _host.Output, StringComparison.Ordinal);
        Assert.DoesNotContain("stale: " + SampleCopy.GeneratedName, _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGraphOutsideTheProjectIsAUsageError()
    {
        Assert.Equal(ExitCodes.Usage, await _host.RunRealAsync("generate", "--graph", "missing.netpc.json"));
        Assert.Contains("missing.netpc.json", _host.Error.ToString(), StringComparison.Ordinal);
        Assert.Empty(_host.Output);
    }

    [Fact]
    public async Task AGraphPathIsResolvedAgainstTheCurrentDirectoryNotTheProjectDirectory()
    {
        string parent = Path.GetDirectoryName(_sample.Directory) ?? _sample.Directory;
        string relative = Path.Combine(Path.GetFileName(_sample.Directory), SampleCopy.GraphName);
        File.WriteAllText(_sample.Combine(SampleCopy.GeneratedName), "// stale\n");

        int fromParent = await new CliTestHost(parent).RunRealAsync("generate", "--check", _sample.Project, "--graph", relative);
        var fromProject = new CliTestHost(_sample.Directory);
        int bareName = await fromProject.RunRealAsync("generate", "--check", "--graph", SampleCopy.GraphName);
        var wrongBase = new CliTestHost(parent);
        int projectRelative = await wrongBase.RunRealAsync("generate", "--check", _sample.Project, "--graph", SampleCopy.GraphName);

        Assert.Equal(ExitCodes.Failed, fromParent);
        Assert.Equal(ExitCodes.Failed, bareName);
        Assert.Equal(ExitCodes.Usage, projectRelative);
    }

    [Fact]
    public async Task AnEmptyGraphValueIsAUsageError()
    {
        Assert.Equal(ExitCodes.Usage, await _host.RunRealAsync("generate", "--graph", ""));
        Assert.DoesNotContain("__default_command", _host.Error.ToString() + _host.Output, StringComparison.Ordinal);
        Assert.Contains("--graph", _host.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AGraphOptionWithoutAValueNeverLeaksSpectresDefaultCommandToken()
    {
        Assert.Equal(ExitCodes.Usage, await _host.RunRealAsync("generate", "--graph"));
        Assert.DoesNotContain("__default_command", _host.Error.ToString() + _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TheDefaultCommandTokenAsAGraphValueIsAUsageError()
    {
        Assert.Equal(ExitCodes.Usage, await _host.RunRealAsync("generate", "--graph", "__default_command"));
        Assert.DoesNotContain("is not a graph", _host.Error.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, "HELLOWORLD.program.netpc.json", true)]
    [InlineData(false, "HELLOWORLD.program.netpc.json", false)]
    [InlineData(false, "HelloWorld.Program.netpc.json", true)]
    public void GraphNamesCompareCaseInsensitivelyOnlyOnCaseInsensitiveFileSystems(bool caseInsensitive, string wanted, bool expected)
    {
        Assert.Equal(expected, GraphPathComparison.For(caseInsensitive).Equals(wanted, SampleCopy.GraphName));
    }

    [Fact]
    public void TheDefaultGraphComparisonFollowsTheOperatingSystem()
    {
        bool caseInsensitive = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS();

        Assert.Equal(caseInsensitive, GraphPathComparison.Default.Equals("a", "A"));
    }

    [Fact]
    public async Task AFailingExtensionFolderExitsOneAndWritesNothing()
    {
        string broken = Directory.CreateDirectory(_sample.Combine("broken-ext")).FullName;
        await File.WriteAllTextAsync(Path.Combine(broken, ExtensionManifest.FileName), "{ not json", TestContext.Current.CancellationToken);
        string csproj = await File.ReadAllTextAsync(_sample.Project, TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            _sample.Project,
            csproj.Replace("</Project>", $"<ItemGroup><NetPrintsExtension Include=\"{broken}\" /></ItemGroup></Project>", StringComparison.Ordinal),
            TestContext.Current.CancellationToken);
        string generated = _sample.Combine(SampleCopy.GeneratedName);
        File.WriteAllText(generated, "// stale\n");

        int exitCode = await _host.RunRealAsync("generate");

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Contains(ExtensionDiagnosticCodes.InvalidManifest, _host.Output, StringComparison.Ordinal);
        Assert.Equal("// stale\n", File.ReadAllText(generated));
    }
}
