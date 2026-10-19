using System.ComponentModel;
using NetPrints.Cli.Infrastructure;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>Settings of <c>extension verify</c>: the extension to check, the host to check it against and the report format.</summary>
internal sealed class ExtensionVerifySettings : CommandSettingsBase
{
    /// <summary>Gets the extension folder or package.</summary>
    [CommandArgument(0, "<path>")]
    [Description("An extension folder (holding netprints-extension.json) or a NetPrintsExtension package (.nupkg).")]
    public string Path { get; init; } = string.Empty;

    /// <summary>Gets the folder of the host assemblies to check against.</summary>
    [CommandOption("--host <folder>")]
    [Description("The folder of the host assemblies to check against; default: the assemblies this tool ships.")]
    public string? Host { get; init; }

    /// <summary>Gets the report format.</summary>
    [CommandOption("--format <format>")]
    [Description("The report format: text (default) or json.")]
    public string Format { get; init; } = ExtensionVerifyCommand.TextFormat;
}
