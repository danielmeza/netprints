using System.ComponentModel;
using NetPrints.Workspace.Run;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Infrastructure;

/// <summary>Settings of a command that builds a project: the project argument and the build configuration.</summary>
internal class ConfigurationSettings : ProjectSettings
{
    /// <summary>Gets the build configuration, passed to the build and to <c>dotnet run</c>.</summary>
    [CommandOption("-c|--configuration <name>")]
    [Description("The build configuration (default Debug).")]
    public string Configuration { get; init; } = RunConfigurations.Debug;

    /// <inheritdoc/>
    public override ValidationResult Validate() =>
        string.IsNullOrWhiteSpace(Configuration)
            ? ValidationResult.Error("The configuration must not be empty.")
            : base.Validate();
}
