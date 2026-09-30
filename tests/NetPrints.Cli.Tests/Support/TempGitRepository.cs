using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NetPrints.Projects;
using Xunit;

namespace NetPrints.Cli.Tests.Support;

/// <summary>A throwaway git repository with a local identity and no signing; never touches the global git configuration.</summary>
internal sealed class TempGitRepository : IDisposable
{
    private static readonly ProcessRunner Runner = new();

    private TempGitRepository(string path) => Path = path;

    public string Path { get; }

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

    public void Dispose() => Directory.Delete(Path, recursive: true);

    public Task<ProcessResult> RunGitAsync(params string[] args) => RunGitAsync(null, args);

    public Task<ProcessResult> RunGitAsync(IReadOnlyDictionary<string, string>? environment, params string[] args) =>
        Runner.RunAsync(new ProcessStartRequest("git", args, Path, environment), TestContext.Current.CancellationToken);

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
