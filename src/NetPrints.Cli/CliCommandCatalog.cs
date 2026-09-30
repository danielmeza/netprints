using System;
using System.Collections.Generic;
using NetPrints.Cli.Commands;
using Spectre.Console.Cli;

namespace NetPrints.Cli;

/// <summary>One command of the tool: its name and how it registers itself with Spectre.</summary>
/// <param name="Name">The command name as typed after <c>netprints</c>.</param>
/// <param name="Register">Adds the command, its aliases and its examples to the configurator.</param>
internal sealed record CliCommand(string Name, Action<IConfigurator> Register);

/// <summary>Every command the tool registers; later batches append their commands here.</summary>
internal static class CliCommandCatalog
{
    private const string SampleProject = "samples/HelloWorld";
    private const string SampleAssembly = "MyLibrary.dll";
    private const string SampleCatalog = "mylibrary.npcat.json";
    private const string SampleCatalogConfig = "netprints.catalog.json";
    private const string SampleGraph = "samples/HelloWorld/HelloWorld.Program.netpc.json";

    /// <summary>Gets all registered commands, in help order.</summary>
    public static IReadOnlyList<CliCommand> All { get; } =
    [
        new(BuildCommand.Name, config => config.AddCommand<BuildCommand>(BuildCommand.Name)
            .WithDescription("Build a NetPrints project.")
            .WithExample(BuildCommand.Name)
            .WithExample(BuildCommand.Name, SampleProject)),
        new(RunCommand.Name, config => config.AddCommand<RunCommand>(RunCommand.Name)
            .WithDescription("Build a NetPrints project and run the program; arguments after -- go to the program.")
            .WithExample(RunCommand.Name)
            .WithExample(RunCommand.Name, SampleProject, "--", "arg1", "arg2")),
        new(GenerateCommand.Name, config => config.AddCommand<GenerateCommand>(GenerateCommand.Name)
            .WithAlias(GenerateCommand.Alias)
            .WithDescription("Regenerate the C# of a project's graphs, or check that it is up to date (alias: regen).")
            .WithExample(GenerateCommand.Name)
            .WithExample(GenerateCommand.Name, SampleProject, "--check")
            .WithExample(GenerateCommand.Alias, "--graph", SampleGraph)),
        new(MigrateCommand.Name, config => config.AddCommand<MigrateCommand>(MigrateCommand.Name)
            .WithDescription("Report the schema version of graph files (no migrations exist yet).")
            .WithExample(MigrateCommand.Name)
            .WithExample(MigrateCommand.Name, SampleGraph)),
        new(CatalogCommand.Name, config => config.AddCommand<CatalogCommand>(CatalogCommand.Name)
            .WithDescription("Build a catalog of the types and members of assemblies or packages, or check that a stored catalog is up to date.")
            .WithExample(CatalogCommand.Name, "--assembly", SampleAssembly, "--output", SampleCatalog)
            .WithExample(CatalogCommand.Name, "--config", SampleCatalogConfig, "--check")
            .WithExample(CatalogCommand.Name, "--package", "Newtonsoft.Json@13.0.3", "--profile", "annotated")),
        new(FormatCommand.Name, config => config.AddCommand<FormatCommand>(FormatCommand.Name)
            .WithDescription("Rewrite graph files into canonical form, or check that they already are.")
            .WithExample(FormatCommand.Name, SampleProject)
            .WithExample(FormatCommand.Name, "--check", SampleProject)),
        new(ShowCommand.Name, config => config.AddCommand<ShowCommand>(ShowCommand.Name)
            .WithDescription("Print a stable, line-oriented summary of a graph file (the text git diffs).")
            .WithExample(ShowCommand.Name, SampleGraph)),
        new(MergeCommand.Name, config => config.AddCommand<MergeCommand>(MergeCommand.Name)
            .WithDescription("The git merge driver for graphs: merge two versions by node, pin and member identity, or fall back to a text merge with conflict markers.")
            .WithExample(MergeCommand.Name, "%O", "%A", "%B")
            .WithExample(MergeCommand.Name, "%O", "%A", "%B", "--marker-size", "9", "--path", "Program.netpc.json")),
    ];
}
