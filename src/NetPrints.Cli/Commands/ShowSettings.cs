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
}
