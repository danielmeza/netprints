namespace NetPrints.Editor.Hosting;

/// <summary>The editor's command line: the project to open and the <c>--profile</c> option (FR-036).</summary>
/// <param name="ProjectPath">The only positional argument, or null when there is none or more than one.</param>
/// <param name="ProfileGiven">Whether <c>--profile</c> was on the command line.</param>
/// <param name="ProfileId">The id after <c>--profile</c>; empty when the option had no value.</param>
internal sealed record StartupArguments(string? ProjectPath, bool ProfileGiven, string ProfileId)
{
    /// <summary>The option that names a project profile for the start page.</summary>
    internal const string ProfileOption = "--profile";

    /// <summary>Reads the command line; <c>--profile &lt;id&gt;</c> and <c>--profile=&lt;id&gt;</c> are both accepted.</summary>
    /// <param name="args">The arguments, or null.</param>
    /// <returns>The parsed arguments.</returns>
    internal static StartupArguments Parse(IReadOnlyList<string>? args)
    {
        List<string> positional = [];
        bool profileGiven = false;
        string profileId = "";
        string withEquals = ProfileOption + "=";
        for (int i = 0; i < (args?.Count ?? 0); i++)
        {
            string argument = args?[i] ?? "";
            if (string.Equals(argument, ProfileOption, StringComparison.Ordinal))
            {
                profileGiven = true;
                profileId = i + 1 < args?.Count ? args[++i] : "";
            }
            else if (argument.StartsWith(withEquals, StringComparison.Ordinal))
            {
                profileGiven = true;
                profileId = argument[withEquals.Length..];
            }
            else
            {
                positional.Add(argument);
            }
        }

        return new StartupArguments(positional is [var single] && !string.IsNullOrWhiteSpace(single) ? single : null, profileGiven, profileId.Trim());
    }
}
