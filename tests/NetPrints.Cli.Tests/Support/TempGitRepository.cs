using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Cli.Tests.Support;

/// <summary>
/// A throwaway git repository with a local identity and no signing. Every git process runs with an empty global configuration, no system
/// configuration and a private <c>XDG_CONFIG_HOME</c>, so the developer's own git setup never reaches a test.
/// </summary>
internal sealed class TempGitRepository : IDisposable
{
    private static readonly ProcessRunner Runner = new();

    private readonly Dictionary<string, string> _isolationVariables;
    private readonly string _isolation = Directory.CreateTempSubdirectory("np-git-home-").FullName;

    private TempGitRepository(string path)
    {
        Path = path;
        string globalConfig = System.IO.Path.Combine(_isolation, "gitconfig");
        System.IO.File.WriteAllText(globalConfig, string.Empty);
        _isolationVariables = new Dictionary<string, string>
        {
            ["GIT_CONFIG_GLOBAL"] = globalConfig,
            ["GIT_CONFIG_NOSYSTEM"] = "1",
            ["XDG_CONFIG_HOME"] = System.IO.Path.Combine(_isolation, "xdg"),
        };
    }

    public string Path { get; }

    /// <summary>The variables that isolate git from the developer's configuration; also handed to the CLI under test.</summary>
    public IReadOnlyDictionary<string, string?> Variables => _isolationVariables.ToDictionary(pair => pair.Key, pair => (string?)pair.Value);

    public static async Task<TempGitRepository> CreateAsync()
    {
        var repository = new TempGitRepository(Directory.CreateTempSubdirectory("np-git-").FullName);
        await repository.GitAsync("init", "-q", "-b", "main");
        await repository.GitAsync("config", "user.name", "Test");
        await repository.GitAsync("config", "user.email", "test@example.com");
        await repository.GitAsync("config", "commit.gpgsign", "false");
        await repository.GitAsync("config", "core.autocrlf", "false");
        return repository;
    }

    public void Dispose()
    {
        Directory.Delete(Path, recursive: true);
        Directory.Delete(_isolation, recursive: true);
    }

    public Task<ProcessResult> RunGitAsync(params string[] args) => RunGitAsync(null, args);

    public Task<ProcessResult> RunGitAsync(IReadOnlyDictionary<string, string>? environment, params string[] args) =>
        Runner.RunAsync(new ProcessStartRequest("git", args, Path, _isolationVariables.Concat(environment ?? new Dictionary<string, string>()).ToDictionary()), TestContext.Current.CancellationToken);

    /// <summary>Runs git and fails the test when it exits non-zero.</summary>
    public async Task<string> GitAsync(params string[] args)
    {
        ProcessResult result = await RunGitAsync(args);
        Assert.True(result.ExitCode == 0, $"git {string.Join(' ', args)} exited {result.ExitCode}: {result.StandardError}");
        return result.StandardOutput;
    }

    /// <summary>Reads a local config value, or <see langword="null"/> when the key is unset.</summary>
    public async Task<string?> ConfigAsync(string key)
    {
        ProcessResult result = await RunGitAsync("config", "--local", "--get", key);
        return result.ExitCode == 0 ? result.StandardOutput.TrimEnd('\n') : null;
    }

    public string File(string relativePath) => System.IO.Path.Combine(Path, relativePath);
}
