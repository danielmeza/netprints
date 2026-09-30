using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetPrints.Cli;
using NetPrints.Cli.Infrastructure;
using NetPrints.Core;
using NetPrints.Projects;
using Spectre.Console;
using Spectre.Console.Testing;
using Xunit;

namespace NetPrints.Cli.Tests.Support;

internal sealed class FakeProjectSystem : IProjectSystem
{
    public BuildResult? Result { get; set; }
    public Exception? ThrowOnBuild { get; set; }
    public ProcessStartRequest RunCommand { get; set; } = new("dotnet", ["run"], "/tmp");
    public List<string> BuiltProjects { get; } = [];
    public IReadOnlyList<string> GraphFiles { get; set; } = [];
    public IReadOnlyList<string> ExtensionFolders { get; set; } = [];
    public List<string> LoadedProjects { get; } = [];

    public Task<ProjectSnapshot> LoadAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        LoadedProjects.Add(projectFilePath);
        return Task.FromResult(new ProjectSnapshot(projectFilePath, "P", "P", "P", BinaryType.Executable, "net10.0", "", false, GraphFiles, ExtensionFolders, [], [], [], "", new Dictionary<string, string>(), []));
    }
    public Task<ProjectSnapshot> ApplyAsync(string projectFilePath, IReadOnlyList<ProjectEdit> edits, CancellationToken cancellationToken) => throw new NotSupportedException();
    public Task<string> CreateAsync(string directory, string projectName, IProjectProfile profile, string rootNamespace, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<BuildResult> BuildAsync(string projectFilePath, CancellationToken cancellationToken)
    {
        BuiltProjects.Add(projectFilePath);
        return ThrowOnBuild is null
            ? Task.FromResult(Result ?? new BuildResult(true, [], null, ""))
            : throw ThrowOnBuild;
    }

    public ProcessStartRequest GetRunCommand(string projectFilePath) => RunCommand;
}

internal sealed class FakeProcessRunner : IProcessRunner
{
    public ProcessResult Result { get; set; } = new(0, "", "");
    public List<ProcessStartRequest> Started { get; } = [];

    public Task<ProcessResult> RunAsync(ProcessStartRequest request, CancellationToken cancellationToken)
    {
        Started.Add(request);
        return Task.FromResult(Result);
    }
}

internal sealed class FakeMsBuildRegistration : IMsBuildRegistration
{
    public bool Available { get; set; } = true;
    public int Calls { get; private set; }

    public bool EnsureRegistered(ILogger logger)
    {
        Calls++;
        return Available;
    }
}

/// <summary>Builds the service collection a CLI test runs against: fakes, a <see cref="TestConsole"/> and a scripted environment.</summary>
internal sealed class CliTestHost
{
    private readonly TestConsole _testConsole = new();

    public CliTestHost(string? currentDirectory = null, IReadOnlyDictionary<string, string?>? variables = null)
    {
        _testConsole.Profile.Width = 500;
        Console = _testConsole;
        Environment = new CliEnvironment(
            currentDirectory ?? System.Environment.CurrentDirectory,
            name => variables is not null && variables.TryGetValue(name, out string? value) ? value : null,
            Error);
    }

    public IAnsiConsole Console { get; init; }
    public StringWriter Error { get; } = new();
    public CliEnvironment Environment { get; }
    public FakeProjectSystem Projects { get; } = new();
    public FakeProcessRunner Processes { get; } = new();
    public FakeMsBuildRegistration MsBuild { get; } = new();

    public string Output => _testConsole.Output;

    public IServiceCollection Services
    {
        get
        {
            var services = new ServiceCollection();
            services.AddSingleton(Environment);
            services.AddSingleton(Console);
            services.AddSingleton<IProcessRunner>(Processes);
            services.AddSingleton<IMsBuildRegistration>(MsBuild);
            services.AddSingleton(new Lazy<IProjectSystem>(() => Projects));
            return services;
        }
    }

    /// <summary>Runs against the real MSBuild project system and process runner, keeping this host's console and environment.</summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>The process exit code.</returns>
    public Task<int> RunRealAsync(params string[] args)
    {
        IServiceCollection services = CliServices.CreateDefault();
        services.AddSingleton(Environment);
        services.AddSingleton(Console);
        return CliApplication.RunAsync(args, services, TestContext.Current.CancellationToken);
    }

    public Task<int> RunAsync(params string[] args) =>
        CliApplication.RunAsync(args, Services, TestContext.Current.CancellationToken);
}
