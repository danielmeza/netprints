using System;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Cli.Tests.Support;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Cli.Tests.Commands;

/// <summary>Failures the project system reports (a restore error, a project that cannot be loaded) map to the documented exit codes in every project command.</summary>
public sealed class ProjectFailureTests : IDisposable
{
    private const string LoadFailure = "The project file could not be loaded.";

    private readonly string _root = Directory.CreateTempSubdirectory("np-fail-").FullName;
    private readonly string _project;
    private readonly CliTestHost _host;

    public ProjectFailureTests()
    {
        _project = Path.Combine(_root, "P.csproj");
        File.WriteAllText(_project, "<Project />");
        _host = new CliTestHost(_root);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    public static TheoryData<string[]> LoadingCommands => new()
    {
        { ["generate"] },
        { ["generate", "--check"] },
        { ["migrate", "P.csproj"] },
    };

    private static ProjectMessage RestoreError() =>
        new(ProjectMessageSeverity.Error, ProjectMessage.RestoreFailed, "restore failed (exit code 1)", "P.csproj", null, null);

    [Theory]
    [MemberData(nameof(LoadingCommands))]
    public async Task AFailedRestoreIsPrintedToStderrAndExitsOne(string[] args)
    {
        _host.Projects.Messages = [RestoreError()];

        Assert.Equal(ExitCodes.Failed, await _host.RunAsync(args));

        Assert.Contains("NPW002: restore failed (exit code 1)", _host.Error.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("up to date", _host.Output, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(LoadingCommands))]
    public async Task AnInformationalProjectMessageDoesNotFailTheCommand(string[] args)
    {
        _host.Projects.Messages = [new ProjectMessage(ProjectMessageSeverity.Info, ProjectMessage.MultiTargetingUsesFirstFramework, "first framework", "P.csproj", null, null)];

        Assert.Equal(ExitCodes.Success, await _host.RunAsync(args));
    }

    public static TheoryData<string[], bool> AllProjectCommands => new()
    {
        { ["build"], false },
        { ["run"], false },
        { ["generate"], true },
        { ["migrate", "P.csproj"], true },
    };

    [Theory]
    [MemberData(nameof(AllProjectCommands))]
    public async Task AProjectThatCannotBeLoadedExitsOneNotFour(string[] args, bool loads)
    {
        var exception = new ProjectSystemException(ProjectSystemException.EvaluationFailed, LoadFailure);
        if (loads)
        {
            _host.Projects.ThrowOnLoad = exception;
        }
        else
        {
            _host.Projects.ThrowOnBuild = exception;
        }

        Assert.Equal(ExitCodes.Failed, await _host.RunAsync(args));

        string error = _host.Error.ToString();
        Assert.Contains(LoadFailure, error, StringComparison.Ordinal);
        Assert.DoesNotContain("Internal error", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ANoSdkProjectSystemExceptionExitsThree()
    {
        _host.Projects.ThrowOnLoad = new ProjectSystemException(ProjectSystemException.NoSdkRegistered, "no sdk");

        Assert.Equal(ExitCodes.NoSdk, await _host.RunAsync("generate"));
    }

    [Fact]
    public async Task AMalformedProjectFileExitsOneThroughTheRealProjectSystem()
    {
        var sample = new SampleCopy();
        try
        {
            File.WriteAllText(sample.Project, "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup>");
            var host = new CliTestHost(sample.Directory);

            Assert.Equal(ExitCodes.Failed, await host.RunRealAsync("generate"));
            Assert.Equal(ExitCodes.Failed, await new CliTestHost(sample.Directory).RunRealAsync("migrate", sample.Project));

            Assert.DoesNotContain("Internal error", host.Error.ToString(), StringComparison.Ordinal);
        }
        finally
        {
            sample.Dispose();
        }
    }
}
