using System.ComponentModel;
using NetPrints.Cli.Infrastructure;
using Spectre.Console;
using Spectre.Console.Cli;

namespace NetPrints.Cli.Commands;

/// <summary>Settings of <c>netprints run</c>: the configuration and the launch profile to run.</summary>
internal sealed class RunCommandSettings : ConfigurationSettings
{
    /// <summary>Gets the launch profile to run; the default profile when <see langword="null"/>. <c>dotnet run</c>'s <c>-lp</c> has no short form here, because Spectre refuses a short option longer than one character.</summary>
    [CommandOption("--launch-profile <name>")]
    [Description("Run the profile with this name from Properties/launchSettings.json (default: the first profile that can run).")]
    public string? LaunchProfile { get; init; }

    /// <summary>Gets a value indicating whether the project runs with no profile.</summary>
    [CommandOption("--no-launch-profile")]
    [Description("Run the project without a launch profile.")]
    public bool NoLaunchProfile { get; init; }

    /// <inheritdoc/>
    public override ValidationResult Validate() =>
        LaunchProfile is not null && NoLaunchProfile
            ? ValidationResult.Error("--launch-profile and --no-launch-profile cannot be used together.")
            : base.Validate();
}
