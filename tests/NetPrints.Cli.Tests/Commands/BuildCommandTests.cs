using System;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Cli.Tests.Support;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Cli.Tests.Commands;

/// <summary>
/// Ported from <c>CliBuildTests</c> (R1-07): exit codes of the build against a fake <see cref="IProjectSystem"/>,
/// so no real MSBuild build or child process is needed. The two cases that ran the program (the child's exit
/// code) belong to <c>RunCommandTests</c> (T023).
/// </summary>
public sealed class BuildCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("np-build-").FullName;
    private readonly string _project;
    private readonly CliTestHost _host;

    public BuildCommandTests()
    {
        _project = Path.Combine(_root, "Program.csproj");
        File.WriteAllText(_project, "<Project />");
        _host = new CliTestHost(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task FailedBuildReturnsExitCode1WithFormattedErrors()
    {
        _host.Projects.Result = new BuildResult(false,
            [
                new ProjectMessage(ProjectMessageSeverity.Error, "CS1002", "; expected", "Program.cs", 3, 7),
                new ProjectMessage(ProjectMessageSeverity.Warning, "CS0168", "unused", "Program.cs", 1, 1),
            ], null, "");

        int exitCode = await _host.RunAsync("build");

        Assert.Equal(ExitCodes.Failed, exitCode);
        string[] lines = _host.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Contains("Program.cs(3,7): CS1002: ; expected", lines);
        Assert.DoesNotContain(lines, line => line.Contains("CS0168", StringComparison.Ordinal));
        Assert.Equal("Build failed with 1 error(s).", lines[^1]);
    }

    [Fact]
    public async Task SuccessfulBuildReturnsExitCode0AndReportsIt()
    {
        int exitCode = await _host.RunAsync("build");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Equal("Build succeeded.", _host.Output.Trim().Split('\n')[^1].Trim());
        Assert.Equal([_project], _host.Projects.BuiltProjects);
        Assert.Empty(_host.Processes.Started);
    }

    [Fact]
    public async Task ThePathArgumentSelectsTheProject()
    {
        string subdirectory = Directory.CreateDirectory(Path.Combine(_root, "Sub")).FullName;
        string other = Path.Combine(subdirectory, "Other.csproj");
        await File.WriteAllTextAsync(other, "<Project />", TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Success, await _host.RunAsync("build", other));

        Assert.Equal([other], _host.Projects.BuiltProjects);
    }

    [Fact]
    public async Task NoCompatibleSdkReturnsExitCode3WithoutBuilding()
    {
        _host.MsBuild.Available = false;

        Assert.Equal(ExitCodes.NoSdk, await _host.RunAsync("build"));

        Assert.Empty(_host.Projects.BuiltProjects);
        Assert.Contains("No .NET SDK", _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ABadProjectArgumentReturnsExitCode2WithoutCheckingTheSdk()
    {
        Assert.Equal(ExitCodes.Usage, await _host.RunAsync("build", Path.Combine(_root, "Missing.csproj")));

        Assert.Equal(0, _host.MsBuild.Calls);
        Assert.Contains("does not exist", _host.Output, StringComparison.Ordinal);
    }
}
