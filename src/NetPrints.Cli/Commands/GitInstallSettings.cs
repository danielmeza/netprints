using System.ComponentModel;
using NetPrints.Cli.Infrastructure;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>Settings of <c>git-install</c>: which git drivers to register, where, and with which command line.</summary>
internal sealed class GitInstallSettings : CommandSettingsBase
{
    /// <summary>The command the driver configuration invokes when <c>--command</c> is not given.</summary>
    public const string DefaultCommand = "netprints";

    /// <summary>Gets a value indicating whether the merge driver is registered as well.</summary>
    [CommandOption("--merge")]
    [Description("Also register the merge driver.")]
    public bool Merge { get; init; }

    /// <summary>Gets a value indicating whether the user's git configuration is changed instead of the repository's.</summary>
    [CommandOption("--global")]
    [Description("Change the user's git configuration and attributes file instead of the repository's.")]
    public bool Global { get; init; }

    /// <summary>Gets the command line the configured drivers run.</summary>
    [CommandOption("--command")]
    [DefaultValue(DefaultCommand)]
    [Description("The command the configuration runs, for example 'dotnet tool run netprints' (default 'netprints').")]
    public string Command { get; init; } = DefaultCommand;

    /// <summary>Gets a value indicating whether what <c>git-install</c> added is removed.</summary>
    [CommandOption("--uninstall")]
    [Description("Remove the configuration keys and the attributes line that git-install added.")]
    public bool Uninstall { get; init; }
}
