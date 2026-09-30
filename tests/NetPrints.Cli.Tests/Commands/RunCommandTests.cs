using System;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Cli.Tests.Support;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Cli.Tests.Commands;

/// <summary>CL-T05: <c>run</c> builds, then runs the program with the arguments after <c>--</c> and returns its exit code.</summary>
public sealed class RunCommandTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("np-run-").FullName;
    private readonly string _project;
    private readonly CliTestHost _host;

    public RunCommandTests()
    {
        _project = Path.Combine(_root, "Program.csproj");
        File.WriteAllText(_project, "<Project />");
        _host = new CliTestHost(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public async Task SuccessfulRunPropagatesTheChildsNonZeroExitCode()
    {
        _host.Processes.Result = new ProcessResult(5, "", "boom");

        int exitCode = await _host.RunAsync("run");

        Assert.Equal(5, exitCode);
        Assert.Contains("boom", _host.Error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuccessfulRunWithZeroChildExitCodeReturnsExitCode0AndForwardsItsOutput()
    {
        _host.Processes.Result = new ProcessResult(0, "Hello, World!", "");

        int exitCode = await _host.RunAsync("run");

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains("Hello, World!", _host.Output, StringComparison.Ordinal);
        Assert.Equal([_project], _host.Projects.BuiltProjects);
    }

    [Fact]
    public async Task ArgumentsAfterTheSeparatorReachTheProgram()
    {
        _host.Projects.RunCommand = new ProcessStartRequest("dotnet", ["run", "--project", _project, "--no-build"], _root);

        int exitCode = await _host.RunAsync("run", "--", "one", "--two", "-r");

        Assert.Equal(ExitCodes.Success, exitCode);
        ProcessStartRequest started = Assert.Single(_host.Processes.Started);
        Assert.Equal(["run", "--project", _project, "--no-build", "--", "one", "--two", "-r"], started.Arguments);
    }

    [Fact]
    public async Task TheProjectArgumentSelectsTheProjectAndArgumentsStillFollow()
    {
        string subdirectory = Directory.CreateDirectory(Path.Combine(_root, "Sub")).FullName;
        string other = Path.Combine(subdirectory, "Other.csproj");
        await File.WriteAllTextAsync(other, "<Project />", TestContext.Current.CancellationToken);

        Assert.Equal(ExitCodes.Success, await _host.RunAsync("run", other, "--", "x"));

        Assert.Equal([other], _host.Projects.BuiltProjects);
        Assert.Equal("x", Assert.Single(_host.Processes.Started).Arguments[^1]);
    }

    [Fact]
    public async Task NoArgumentsAddsNoSeparator()
    {
        await _host.RunAsync("run");

        Assert.DoesNotContain("--", Assert.Single(_host.Processes.Started).Arguments);
    }

    [Fact]
    public async Task BuildFailureReturnsExitCode1WithoutRunning()
    {
        _host.Projects.Result = new BuildResult(false, [new ProjectMessage(ProjectMessageSeverity.Error, "CS1002", "; expected", "Program.cs", 3, 7)], null, "");

        int exitCode = await _host.RunAsync("run");

        Assert.Equal(ExitCodes.Failed, exitCode);
        Assert.Empty(_host.Processes.Started);
        Assert.Contains("Build failed with 1 error(s).", _host.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NoCompatibleSdkReturnsExitCode3()
    {
        _host.MsBuild.Available = false;

        Assert.Equal(ExitCodes.NoSdk, await _host.RunAsync("run"));
        Assert.Empty(_host.Processes.Started);
    }
}
