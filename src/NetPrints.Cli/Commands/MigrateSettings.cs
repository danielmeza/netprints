using System.ComponentModel;
using NetPrints.Cli.Infrastructure;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>Settings of <c>migrate</c>: the graph files, directories or projects to check.</summary>
internal sealed class MigrateSettings : CommandSettingsBase
{
    /// <summary>Gets the graph files, directories searched recursively for graphs, or <c>.csproj</c> files (their graphs).</summary>
    [CommandArgument(0, "[paths]")]
    [Description("Graph files, directories searched for graphs, or projects. Defaults to the current directory's project.")]
    public string[] Paths { get; init; } = [];
}
