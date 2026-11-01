namespace NetPrints.Workspace.Run;

/// <summary>
/// The stable codes of the run profile diagnostics (<c>NPR</c> prefix), listed in <c>docs/guide/diagnostics.md</c>.
/// </summary>
public static class NprDiagnosticCodes
{
    /// <summary>NPR001: <c>Properties/launchSettings.json</c> is not valid launch settings.</summary>
    public const string LaunchSettingsInvalid = "NPR001";

    /// <summary>NPR002: two profile names differ only by case.</summary>
    public const string ProfileNamesDifferByCase = "NPR002";

    /// <summary>NPR003: the profile's <c>commandName</c> is a kind NetPrints cannot run.</summary>
    public const string KindNotRunnable = "NPR003";

    /// <summary>NPR004: <c>netprints run --launch-profile</c> names no profile of the file.</summary>
    public const string UnknownProfile = "NPR004";

    /// <summary>NPR005: a <c>%NAME%</c> in a path field is not set.</summary>
    public const string PathVariableUnset = "NPR005";

    /// <summary>NPR006: a <c>$(Name)</c> the project does not define (an error in a path, a warning elsewhere).</summary>
    public const string PropertyUndefined = "NPR006";

    /// <summary>NPR007: a <c>%NAME%</c> in the arguments or environment is not set.</summary>
    public const string ArgumentVariableUnset = "NPR007";

    /// <summary>NPR008: the executable was not found, or <c>executablePath</c> is empty.</summary>
    public const string ExecutableNotFound = "NPR008";

    /// <summary>NPR009: a <c>.cmd</c> or <c>.bat</c> executable is refused on Windows.</summary>
    public const string ScriptTargetRefused = "NPR009";

    /// <summary>NPR010: a macOS app bundle names no executable in <c>Contents/Info.plist</c>.</summary>
    public const string AppBundleUnresolved = "NPR010";

    /// <summary>NPR011: a <c>Project</c> profile on a project that is not an executable.</summary>
    public const string ProjectKindNeedsExecutable = "NPR011";
}
