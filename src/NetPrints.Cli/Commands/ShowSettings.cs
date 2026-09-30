using System.ComponentModel;
using NetPrints.Cli.Infrastructure;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>Settings of <c>show</c>: the graph file to summarize.</summary>
internal sealed class ShowSettings : CommandSettingsBase
{
    /// <summary>Gets the graph file.</summary>
    [CommandArgument(0, "<graph>")]
    [Description("The .netpc.json graph file to summarize.")]
    public string Graph { get; init; } = string.Empty;

    /// <summary>Gets a value indicating whether a file that is not a readable graph is printed as its raw text, exiting 0 (the mode git uses for diffs).</summary>
    [CommandOption("--textconv")]
    [Description("Print a file that cannot be read as a graph as its raw text and exit 0, so git diff never fails; git-install registers show with this option.")]
    public bool TextConv { get; init; }
}
