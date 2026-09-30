using System.ComponentModel;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Infrastructure;

/// <summary>The options every command shares.</summary>
internal abstract class CommandSettingsBase : CommandSettings
{
    /// <summary>Gets a value indicating whether logs are written at Information level and internal errors show their stack trace.</summary>
    [CommandOption("--verbose")]
    [Description("Write informational logs and internal-error stack traces to stderr.")]
    public bool Verbose { get; init; }
}
