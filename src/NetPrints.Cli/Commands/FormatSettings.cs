using System.ComponentModel;
using NetPrints.Cli.Infrastructure;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>Settings of <c>format</c>: the graph files or directories to canonicalize, and whether to only check them.</summary>
internal sealed class FormatSettings : CommandSettingsBase
{
    /// <summary>Gets the graph files, or directories searched recursively for graphs.</summary>
    [CommandArgument(0, "[paths]")]
    [Description("Graph files or directories searched for graphs. Defaults to the current directory.")]
    public string[] Paths { get; init; } = [];

    /// <summary>Gets a value indicating whether nothing is written and the exit code reports graphs that are not canonical.</summary>
    [CommandOption("--check")]
    [Description("Write nothing; list graphs that are not canonical or unreadable and exit 1 if there are any.")]
    public bool Check { get; init; }
}
