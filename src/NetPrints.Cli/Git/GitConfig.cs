using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NetPrints.Projects;

namespace NetPrints.Cli.Git;

/// <summary>The outcome of a <c>git config</c> write.</summary>
/// <param name="Changed">Whether a value was written or removed.</param>
/// <param name="Error">What git reported when it failed, or <see langword="null"/>.</param>
internal sealed record GitConfigChange(bool Changed, string? Error);

/// <summary>Reads and writes one git configuration scope (the repository's or the user's) through <c>git config</c> (contracts/git.md §3).</summary>
/// <param name="processes">Runs <c>git</c>.</param>
/// <param name="workingDirectory">The directory git runs in; the repository when <paramref name="global"/> is <see langword="false"/>.</param>
/// <param name="global">Whether the user's configuration is addressed instead of the repository's.</param>
/// <param name="environment">Environment variables git runs with on top of the process's own, or <see langword="null"/>.</param>
internal sealed class GitConfig(IProcessRunner processes, string workingDirectory, bool global, IReadOnlyDictionary<string, string>? environment)
{
    private const int KeyNotFound = 1;
    private const int UnsetKeyNotFound = 5;

    /// <summary>Reads a value.</summary>
    /// <param name="key">The dotted key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The value, or <see langword="null"/> when the key is not set.</returns>
    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken)
    {
        ProcessResult result = await RunAsync(["--get", key], cancellationToken).ConfigureAwait(false);
        return result.ExitCode == 0 ? result.StandardOutput.TrimEnd('\n', '\r') : null;
    }

    /// <summary>Writes a value.</summary>
    /// <param name="key">The dotted key.</param>
    /// <param name="value">The value.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether it succeeded, with git's message when not.</returns>
    public async Task<GitConfigChange> SetAsync(string key, string value, CancellationToken cancellationToken)
    {
        ProcessResult result = await RunAsync([key, value], cancellationToken).ConfigureAwait(false);
        return new GitConfigChange(result.ExitCode == 0, result.ExitCode == 0 ? null : Describe(result));
    }

    /// <summary>Removes a key.</summary>
    /// <param name="key">The dotted key.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Whether the key was removed; a key that was not set is not an error.</returns>
    public async Task<GitConfigChange> UnsetAsync(string key, CancellationToken cancellationToken)
    {
        ProcessResult result = await RunAsync(["--unset", key], cancellationToken).ConfigureAwait(false);
        return result.ExitCode switch
        {
            0 => new GitConfigChange(true, null),
            UnsetKeyNotFound or KeyNotFound => new GitConfigChange(false, null),
            _ => new GitConfigChange(false, Describe(result)),
        };
    }

    private static string Describe(ProcessResult result) => $"git config failed with exit code {result.ExitCode}: {result.StandardError.Trim()}";

    private Task<ProcessResult> RunAsync(string[] arguments, CancellationToken cancellationToken) =>
        processes.RunAsync(new ProcessStartRequest("git", ["config", global ? "--global" : "--local", .. arguments], workingDirectory, environment), cancellationToken);
}
