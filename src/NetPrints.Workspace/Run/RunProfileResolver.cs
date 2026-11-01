using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NetPrints.Core;
using NetPrints.Projects;

namespace NetPrints.Workspace.Run;

/// <summary>Turns a launch profile and the project's state into the process to start, or into the problems that stop the run.</summary>
public static partial class RunProfileResolver
{
    private const string PathVariable = "PATH";
    private const string LaunchProfileVariable = "DOTNET_LAUNCH_PROFILE";
    private const string TokenPattern = @"%(?<env>[^%\s]+)%|\$\((?<prop>[A-Za-z_][A-Za-z0-9_-]*)\)";

    /// <summary>Resolves a run: choose the profile, expand, split, resolve paths, build the request.</summary>
    /// <param name="inputs">The inputs, read beforehand.</param>
    /// <param name="platform">The operating system rules.</param>
    /// <returns>The resolution; its <see cref="RunResolution.Request"/> is <see langword="null"/> when an error problem blocks the run.</returns>
    public static RunResolution Resolve(RunInputs inputs, RunPlatform platform)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        ArgumentNullException.ThrowIfNull(platform);

        RunProfileChoice choice = Choose(inputs.LaunchSettings, inputs.OutputType, inputs.ProfileName, inputs.NoLaunchProfile);
        if (choice.IsBlocked)
        {
            return new RunResolution { Problems = choice.Problems };
        }

        string projectFolder = ProjectFolder(inputs.ProjectFilePath);
        if (choice.Profile is not { } profile)
        {
            IReadOnlyList<string> tail = inputs.ArgumentOverride ?? [];
            return new RunResolution { Request = ProjectRequest(inputs, null, [], tail, projectFolder) };
        }

        var problems = new List<RunProblem>();
        var expander = new Expander(inputs, platform, profile, problems);
        bool runsExecutable = string.Equals(profile.CommandName, RunProfileKinds.Executable, StringComparison.Ordinal);
        string executableText = runsExecutable ? expander.Expand(profile.ExecutablePath ?? string.Empty, "executablePath", isPath: true) : string.Empty;
        string argumentsText = inputs.ArgumentOverride is null ? expander.Expand(profile.CommandLineArgs ?? string.Empty, "commandLineArgs", isPath: false) : string.Empty;
        string workingText = expander.Expand(profile.WorkingDirectory ?? string.Empty, "workingDirectory", isPath: true);
        List<KeyValuePair<string, string>> variables = [.. profile.EnvironmentVariables.Select(pair => KeyValuePair.Create(pair.Key, expander.Expand(pair.Value, "environmentVariables." + pair.Key, isPath: false)))];
        if (problems.Any(problem => problem.Severity == RunProblemSeverity.Error))
        {
            return new RunResolution { Profile = profile, Problems = problems };
        }

        string workingDirectory = string.IsNullOrEmpty(workingText) ? projectFolder : FullPath(Path.Combine(projectFolder, workingText));
        IReadOnlyList<string> arguments = inputs.ArgumentOverride ?? (argumentsText.Length == 0 ? [] : CommandLineSplitter.Split(argumentsText));
        string[] added = [.. variables.Select(pair => pair.Key), LaunchProfileVariable];
        if (!runsExecutable)
        {
            ProcessStartRequest dotnet = ProjectRequest(inputs, profile.Name, variables, arguments, workingDirectory);
            return new RunResolution { Profile = profile, Request = dotnet, Problems = problems, AddedEnvironment = added };
        }

        string? executable = ResolveExecutable(executableText, workingDirectory, platform, expander.Environment, profile.Name, problems);
        if (executable is null)
        {
            return new RunResolution { Profile = profile, Problems = problems };
        }

        ProcessStartRequest request = ExecutableRequest(inputs, platform, profile.Name, executable, arguments, workingDirectory, variables);
        return new RunResolution { Profile = profile, Request = request, Problems = problems, AddedEnvironment = added };
    }

    /// <summary>Chooses the profile to run.</summary>
    /// <param name="settings">The launch settings.</param>
    /// <param name="outputType">The project's output type.</param>
    /// <param name="profileName">The profile asked for; <see langword="null"/> for the default.</param>
    /// <param name="noLaunchProfile">Whether the project runs with no profile.</param>
    /// <returns>The profile (or none), or the problems that block the choice and nothing else.</returns>
    public static RunProfileChoice Choose(LaunchSettings settings, BinaryType outputType, string? profileName, bool noLaunchProfile)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.IsUsable)
        {
            return new RunProfileChoice(null, settings.Problems);
        }

        if (noLaunchProfile)
        {
            return new RunProfileChoice(null, []);
        }

        if (profileName is null)
        {
            return new RunProfileChoice(settings.DefaultProfile(outputType), []);
        }

        LaunchProfile? profile = settings.Find(profileName);
        if (profile is null)
        {
            return new RunProfileChoice(null, [Error(NprDiagnosticCodes.UnknownProfile, $"No launch profile is named '{profileName}'.")]);
        }

        RunProfileAvailability availability = profile.AvailabilityFor(outputType);
        return availability.CanRun || availability.Reason is null
            ? new RunProfileChoice(profile, [])
            : new RunProfileChoice(null, [availability.Reason]);
    }

    /// <summary>Lists the project properties a profile reads.</summary>
    /// <param name="profile">The profile.</param>
    /// <returns>The distinct <c>$(Name)</c> names in <c>executablePath</c>, <c>commandLineArgs</c>, <c>workingDirectory</c> and the environment values, first spelling kept.</returns>
    public static IReadOnlyList<string> PropertyNames(LaunchProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        IEnumerable<string?> fields = [profile.ExecutablePath, profile.CommandLineArgs, profile.WorkingDirectory, .. profile.EnvironmentVariables.Select(pair => pair.Value)];
        return [.. fields
            .Where(field => !string.IsNullOrEmpty(field))
            .SelectMany(field => Token().Matches(field ?? string.Empty).Select(match => match.Groups["prop"]).Where(group => group.Success).Select(group => group.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)];
    }

    private static RunProblem Error(string code, string message) => new(code, RunProblemSeverity.Error, message);

    private static string ProjectFolder(string projectFilePath) =>
        Path.GetDirectoryName(projectFilePath) is { Length: > 0 } folder ? folder : Directory.GetCurrentDirectory();

    private static string FullPath(string path)
    {
        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }

    private static string? ResolveExecutable(
        string value,
        string workingDirectory,
        RunPlatform platform,
        IReadOnlyDictionary<string, string> environment,
        string profileName,
        List<RunProblem> problems)
    {
        if (value.Length == 0)
        {
            problems.Add(Error(NprDiagnosticCodes.ExecutableNotFound, $"The executablePath of profile '{profileName}' is empty."));
            return null;
        }

        string? path = platform.HasDirectorySeparator(value) ? FullPath(Path.Combine(workingDirectory, value)) : FindOnPath(value, platform, environment);
        if (path is null)
        {
            problems.Add(NotFound(value, profileName));
            return null;
        }

        if (platform.IsBundle(path))
        {
            string? inside = platform.BundleExecutable(path);
            if (inside is null)
            {
                problems.Add(Error(NprDiagnosticCodes.AppBundleUnresolved, $"'{path}' is an app bundle with no executable named in Contents/Info.plist (profile '{profileName}')."));
                return null;
            }

            path = inside;
        }

        if (platform.RefusesTarget(path))
        {
            problems.Add(Error(
                NprDiagnosticCodes.ScriptTargetRefused,
                $"'{path}' runs through cmd.exe, which parses the arguments again; set the interpreter as the executable path and the script as its first argument (profile '{profileName}')."));
            return null;
        }

        if (!File.Exists(path))
        {
            problems.Add(NotFound(path, profileName));
            return null;
        }

        return path;
    }

    private static RunProblem NotFound(string path, string profileName) =>
        Error(NprDiagnosticCodes.ExecutableNotFound, $"The executable '{path}' was not found (profile '{profileName}').");

    private static string? FindOnPath(string name, RunPlatform platform, IReadOnlyDictionary<string, string> environment)
    {
        if (!environment.TryGetValue(PathVariable, out string? list))
        {
            return null;
        }

        foreach (string raw in list.Split(platform.PathListSeparator))
        {
            string entry = raw.Trim().Trim('"');
            if (entry.Length == 0 || !Path.IsPathRooted(entry))
            {
                continue;
            }

            foreach (string candidate in platform.CandidateNames(name))
            {
                string path = Path.Combine(entry, candidate);
                if (File.Exists(path))
                {
                    return FullPath(path);
                }
            }
        }

        return null;
    }

    private static ProcessStartRequest ProjectRequest(
        RunInputs inputs,
        string? profileName,
        IReadOnlyList<KeyValuePair<string, string>> variables,
        IReadOnlyList<string> arguments,
        string workingDirectory)
    {
        List<string> list = ["run", "--project", inputs.ProjectFilePath, "--no-build", "--no-launch-profile", "-c", inputs.Configuration];
        foreach ((string name, string value) in variables)
        {
            list.AddRange(["-e", $"{name}={value}"]);
        }

        if (profileName is not null)
        {
            list.AddRange(["-e", $"{LaunchProfileVariable}={profileName}"]);
        }

        if (arguments.Count > 0)
        {
            list.Add("--");
            list.AddRange(arguments);
        }

        var setBack = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (MsBuildVariable variable in inputs.MsBuildVariables.Where(variable => !string.IsNullOrEmpty(variable.Name) && variable.PriorValue is not null))
        {
            setBack[variable.Name] = variable.PriorValue ?? string.Empty;
        }

        return new ProcessStartRequest("dotnet", list, workingDirectory, setBack)
        {
            EnvironmentRemovals = [.. inputs.MsBuildVariables.Where(variable => !string.IsNullOrEmpty(variable.Name) && variable.PriorValue is null).Select(variable => variable.Name)],
        };
    }

    private static ProcessStartRequest ExecutableRequest(
        RunInputs inputs,
        RunPlatform platform,
        string profileName,
        string executable,
        IReadOnlyList<string> arguments,
        string workingDirectory,
        IReadOnlyList<KeyValuePair<string, string>> variables)
    {
        var environment = new Dictionary<string, string>(platform.VariableNameComparer);
        var profileNames = new HashSet<string>(platform.VariableNameComparer) { LaunchProfileVariable };
        foreach ((string name, string value) in variables)
        {
            environment[name] = value;
            profileNames.Add(name);
        }

        environment[LaunchProfileVariable] = profileName;
        var removals = new List<string>();
        foreach (MsBuildVariable variable in inputs.MsBuildVariables.Where(variable => !string.IsNullOrEmpty(variable.Name) && !profileNames.Contains(variable.Name)))
        {
            if (variable.PriorValue is { } prior)
            {
                environment[variable.Name] = prior;
            }
            else
            {
                removals.Add(variable.Name);
            }
        }

        return new ProcessStartRequest(executable, arguments, workingDirectory, environment) { EnvironmentRemovals = removals };
    }

    private sealed class Expander(RunInputs inputs, RunPlatform platform, LaunchProfile profile, List<RunProblem> problems)
    {
        private readonly Dictionary<string, string> properties = new(inputs.ProjectProperties, StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, string> Environment { get; } = Snapshot(inputs.Environment, platform.VariableNameComparer);

        public string Expand(string text, string field, bool isPath) =>
            Token().Replace(text, match => match.Groups["env"].Success
                ? Variable(match, field, isPath)
                : Property(match, field, isPath));

        private static Dictionary<string, string> Snapshot(IReadOnlyDictionary<string, string> source, StringComparer comparer)
        {
            var copy = new Dictionary<string, string>(comparer);
            foreach ((string name, string value) in source)
            {
                copy[name] = value;
            }

            return copy;
        }

        private string Variable(Match match, string field, bool isPath)
        {
            string name = match.Groups["env"].Value;
            if (Environment.TryGetValue(name, out string? value))
            {
                return value;
            }

            problems.Add(isPath
                ? Error(NprDiagnosticCodes.PathVariableUnset, $"The variable {name} is not set ({field} of profile '{profile.Name}').")
                : new RunProblem(NprDiagnosticCodes.ArgumentVariableUnset, RunProblemSeverity.Warning, $"The variable {name} is not set ({field} of profile '{profile.Name}'); the text stays as written."));
            return match.Value;
        }

        private string Property(Match match, string field, bool isPath)
        {
            string name = match.Groups["prop"].Value;
            if (properties.TryGetValue(name, out string? value))
            {
                return value;
            }

            string message = $"The project does not define the property {name} ({field} of profile '{profile.Name}').";
            problems.Add(new RunProblem(NprDiagnosticCodes.PropertyUndefined, isPath ? RunProblemSeverity.Error : RunProblemSeverity.Warning, message));
            return match.Value;
        }
    }

    [GeneratedRegex(TokenPattern, RegexOptions.CultureInvariant)]
    private static partial Regex Token();
}
