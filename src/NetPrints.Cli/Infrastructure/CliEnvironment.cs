using System;
using System.IO;

namespace NetPrints.Cli.Infrastructure;

/// <summary>
/// The process state a command reads: the current directory, environment variables and the standard-error
/// writer. Injected so tests script them instead of touching the real process.
/// </summary>
internal sealed class CliEnvironment
{
    private readonly Func<string, string?> _getVariable;

    /// <summary>Creates an environment from explicit values.</summary>
    /// <param name="currentDirectory">The directory relative paths and the implicit project resolve against.</param>
    /// <param name="getVariable">Reads an environment variable; returns <see langword="null"/> when unset.</param>
    /// <param name="error">The writer for logs and internal-error details.</param>
    public CliEnvironment(string currentDirectory, Func<string, string?> getVariable, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(currentDirectory);
        ArgumentNullException.ThrowIfNull(getVariable);
        ArgumentNullException.ThrowIfNull(error);
        CurrentDirectory = currentDirectory;
        _getVariable = getVariable;
        Error = error;
    }

    /// <summary>Gets the directory relative paths and the implicit project resolve against.</summary>
    public string CurrentDirectory { get; }

    /// <summary>Gets the writer for logs and internal-error details (stderr in the real process).</summary>
    public TextWriter Error { get; }

    /// <summary>Creates the environment of the running process.</summary>
    /// <returns>An environment over <see cref="Environment"/> and <see cref="Console.Error"/>.</returns>
    public static CliEnvironment FromProcess() =>
        new(Environment.CurrentDirectory, Environment.GetEnvironmentVariable, Console.Error);

    /// <summary>Reads an environment variable.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The value, or <see langword="null"/> when unset.</returns>
    public string? GetVariable(string name) => _getVariable(name);
}
