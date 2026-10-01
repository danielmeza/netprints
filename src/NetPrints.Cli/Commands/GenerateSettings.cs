using System;
using System.ComponentModel;
using NetPrints.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>Settings of <c>generate</c>: the project, whether to only check, and the graphs to limit the run to.</summary>
internal sealed class GenerateSettings : ProjectSettings
{
    // The name Spectre gives the implicit command; it shows up as the value of an option written without one.
    private const string DefaultCommandToken = "__default_command";

    /// <summary>Gets a value indicating whether nothing is written and stale generated files make the command fail.</summary>
    [CommandOption("--check")]
    [Description("Write no generated file; exit 1 and list the generated files that are stale or missing.")]
    public bool Check { get; init; }

    /// <summary>Gets the graph files of the project the run is limited to; every graph of the project when empty.</summary>
    [CommandOption("--graph <file>")]
    [Description("Limit the run to this graph file of the project (repeatable).")]
    public string[] Graphs { get; init; } = [];

    /// <inheritdoc/>
    public override ValidationResult Validate()
    {
        foreach (string graph in Graphs)
        {
            if (string.IsNullOrWhiteSpace(graph) || string.Equals(graph, DefaultCommandToken, StringComparison.Ordinal))
            {
                return ValidationResult.Error("--graph needs the path of a graph file of the project.");
            }
        }

        return base.Validate();
    }
}
