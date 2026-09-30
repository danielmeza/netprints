using System.ComponentModel;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Infrastructure;

/// <summary>Settings of a command that acts on one project: the optional project argument.</summary>
internal class ProjectSettings : CommandSettingsBase
{
    /// <summary>Gets the project file or the directory holding it; the current directory's project when omitted.</summary>
    [CommandArgument(0, "[project]")]
    [Description("The .csproj file, or the directory holding a single one. Defaults to the current directory.")]
    public string? Project { get; init; }
}
