using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NetPrints.Core;

namespace NetPrints.Workspace.Run;

/// <summary>
/// Plans a run for both front ends: reads the launch settings, evaluates the project's properties at most once and resolves the profile.
/// </summary>
public sealed class RunPlanner
{
    private const string OutputTypeProperty = "OutputType";
    private const string NoValue = "(none)";

    private readonly IRunProjectSystem projects;
    private readonly RunPlatform platform;
    private readonly Func<IReadOnlyDictionary<string, string>> environment;
    private readonly Func<IReadOnlyList<MsBuildVariable>> msBuildVariables;
    private readonly ILogger logger;

    /// <summary>Creates a planner.</summary>
    /// <param name="projects">Evaluates the project's properties.</param>
    /// <param name="platform">The operating system rules.</param>
    /// <param name="environment">Returns a snapshot of the process environment, taken at each plan.</param>
    /// <param name="msBuildVariables">Returns the variables NetPrints' MSBuild registration added or overwrote.</param>
    /// <param name="logger">Receives event 4006; defaults to no logging.</param>
    public RunPlanner(
        IRunProjectSystem projects,
        RunPlatform platform,
        Func<IReadOnlyDictionary<string, string>> environment,
        Func<IReadOnlyList<MsBuildVariable>> msBuildVariables,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(projects);
        ArgumentNullException.ThrowIfNull(platform);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(msBuildVariables);

        this.projects = projects;
        this.platform = platform;
        this.environment = environment;
        this.msBuildVariables = msBuildVariables;
        this.logger = logger ?? NullLogger.Instance;
    }

    /// <summary>Creates the planner of the running process: its platform, its environment and MSBuild's variable record.</summary>
    /// <param name="projects">Evaluates the project's properties.</param>
    /// <param name="logger">Receives event 4006; defaults to no logging.</param>
    /// <returns>The planner.</returns>
    public static RunPlanner Create(IRunProjectSystem projects, ILogger? logger = null)
    {
        RunPlatform current = RunPlatform.Current;
        return new RunPlanner(projects, current, () => SnapshotEnvironment(current), () => MsBuildRegistration.Variables, logger);
    }

    /// <summary>Reads the launch settings of a project.</summary>
    /// <param name="projectFilePath">The full path of the project file.</param>
    /// <returns>The settings; <see cref="LaunchSettings.None"/> when the file is absent or the path has no folder.</returns>
    public LaunchSettings ReadLaunchSettings(string projectFilePath)
    {
        ArgumentNullException.ThrowIfNull(projectFilePath);
        return Path.GetDirectoryName(projectFilePath) is { Length: > 0 } directory ? LaunchSettingsReader.Read(directory) : LaunchSettings.None;
    }

    /// <summary>Plans a run.</summary>
    /// <param name="request">What to run.</param>
    /// <param name="cancellationToken">Cancels the evaluation.</param>
    /// <returns>The resolution; blocked when an error problem stops the run before the build.</returns>
    public async Task<RunResolution> PlanAsync(RunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        LaunchSettings settings = ReadLaunchSettings(request.ProjectFilePath);
        BinaryType outputType;
        IReadOnlyDictionary<string, string> properties;
        if (request.OutputType is { } known)
        {
            outputType = known;
            RunProfileChoice choice = RunProfileResolver.Choose(settings, known, request.ProfileName, request.NoLaunchProfile);
            if (choice.IsBlocked)
            {
                return new RunResolution { Problems = choice.Problems };
            }

            IReadOnlyList<string> names = choice.Profile is { } profile ? RunProfileResolver.PropertyNames(profile) : [];
            properties = names.Count == 0
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : await projects.EvaluatePropertiesAsync(request.ProjectFilePath, request.Configuration, names, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            IReadOnlyList<string> names = [.. new[] { OutputTypeProperty }.Concat(settings.Profiles.SelectMany(RunProfileResolver.PropertyNames)).Distinct(StringComparer.OrdinalIgnoreCase)];
            properties = await projects.EvaluatePropertiesAsync(request.ProjectFilePath, request.Configuration, names, cancellationToken).ConfigureAwait(false);
            outputType = OutputTypes.FromProperty(properties.TryGetValue(OutputTypeProperty, out string? value) ? value : null);
        }

        var inputs = new RunInputs
        {
            ProjectFilePath = request.ProjectFilePath,
            OutputType = outputType,
            Configuration = request.Configuration,
            LaunchSettings = settings,
            ProfileName = request.ProfileName,
            NoLaunchProfile = request.NoLaunchProfile,
            ArgumentOverride = request.ArgumentOverride,
            ProjectProperties = properties,
            Environment = environment(),
            MsBuildVariables = msBuildVariables(),
        };
        RunResolution resolution = RunProfileResolver.Resolve(inputs, platform);
        if (resolution.Request is { } started)
        {
            Log.RunResolved(logger, resolution.Profile?.Name ?? NoValue, resolution.Profile?.CommandName ?? NoValue, started.FileName, started.Arguments.Count, started.WorkingDirectory);
        }

        return resolution;
    }

    private static Dictionary<string, string> SnapshotEnvironment(RunPlatform platform)
    {
        var snapshot = new Dictionary<string, string>(platform.VariableNameComparer);
        foreach (DictionaryEntry entry in Environment.GetEnvironmentVariables())
        {
            if (entry.Key is string name && entry.Value is string value)
            {
                snapshot[name] = value;
            }
        }

        return snapshot;
    }
}
