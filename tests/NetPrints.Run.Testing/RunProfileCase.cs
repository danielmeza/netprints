using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Workspace;
using NetPrints.Workspace.Run;

namespace NetPrints.Run.Testing;

/// <summary>
/// One run scenario: the files of a project, the inputs a front end gives the planner, and the problems and process request it must get.
/// Any string may hold the token <c>{root}</c>, replaced by the folder the case is materialized in.
/// </summary>
/// <param name="Name">The case's name; unique in <see cref="RunProfileCases"/>.</param>
public sealed record RunProfileCase(string Name)
{
    private const string RootToken = "{root}";

    /// <summary>Gets the platform whose rules the case runs under.</summary>
    public required RunPlatform Platform { get; init; }

    /// <summary>Gets the text of <c>Properties/launchSettings.json</c>; <see langword="null"/> for no file.</summary>
    public string? LaunchSettingsJson { get; init; }

    /// <summary>Gets the profile asked for; <see langword="null"/> for the default.</summary>
    public string? ProfileName { get; init; }

    /// <summary>Gets a value indicating whether the project runs with no profile.</summary>
    public bool NoLaunchProfile { get; init; }

    /// <summary>Gets the project's output type.</summary>
    public BinaryType OutputType { get; init; } = BinaryType.Executable;

    /// <summary>Gets the build configuration.</summary>
    public string Configuration { get; init; } = RunConfigurations.Debug;

    /// <summary>Gets the process environment.</summary>
    public IReadOnlyDictionary<string, string> Environment { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the evaluated project properties.</summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } = new Dictionary<string, string>();

    /// <summary>Gets the files to write: path relative to the root, then content (empty for an empty file).</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Files { get; init; } = [];

    /// <summary>Gets the arguments that replace the profile's own; <see langword="null"/> keeps them.</summary>
    public IReadOnlyList<string>? ArgumentOverride { get; init; }

    /// <summary>Gets the variables MSBuild's registration added or overwrote.</summary>
    public IReadOnlyList<MsBuildVariable> MsBuildVariables { get; init; } = [];

    /// <summary>Gets the problem codes expected, in order.</summary>
    public IReadOnlyList<string> ExpectedProblems { get; init; } = [];

    /// <summary>Gets the request expected; <see langword="null"/> when the run is blocked.</summary>
    public ProcessStartRequest? ExpectedRequest { get; init; }

    /// <summary>Gets a value indicating whether the case uses an input the editor does not have.</summary>
    public bool CliOnly { get; init; }

    /// <summary>Gets a value indicating whether the case puts the root into a <c>:</c> separated <c>PATH</c>, which a root holding a drive letter breaks.</summary>
    public bool NeedsColonFreeRoot { get; init; }

    /// <summary>Gets the folder the case was materialized in; <see langword="null"/> before <see cref="Materialize"/>.</summary>
    public string? Root { get; init; }

    /// <summary>Gets the full path of the case's project file; the case must be materialized.</summary>
    public string ProjectFilePath => Path.Combine(RequireRoot(), "App", "App.csproj");

    /// <summary>Gets why the case cannot run on this host; <see langword="null"/> when it can. The case must be materialized.</summary>
    public string? SkipReason =>
        NeedsColonFreeRoot && RequireRoot().Contains(':', StringComparison.Ordinal)
            ? "The case needs a root with no ':' in a ':' separated PATH, and this host's temp folder has a drive letter."
            : null;

    /// <summary>Writes the project and the case's files under <paramref name="root"/> and replaces <c>{root}</c> everywhere.</summary>
    /// <param name="root">An existing, empty folder.</param>
    /// <returns>The case with the token replaced and <see cref="Root"/> set.</returns>
    public RunProfileCase Materialize(string root)
    {
        ArgumentException.ThrowIfNullOrEmpty(root);

        string appFolder = Directory.CreateDirectory(Path.Combine(root, "App")).FullName;
        File.WriteAllText(Path.Combine(appFolder, "App.csproj"), "<Project Sdk=\"Microsoft.NET.Sdk\" />");
        string? json = LaunchSettingsJson?.Replace(RootToken, root.Replace("\\", "\\\\", StringComparison.Ordinal), StringComparison.Ordinal);
        if (json is not null)
        {
            string properties = Directory.CreateDirectory(Path.Combine(appFolder, "Properties")).FullName;
            File.WriteAllText(Path.Combine(properties, "launchSettings.json"), json);
        }

        foreach ((string relative, string content) in Files)
        {
            string path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? root);
            File.WriteAllText(path, content.Replace(RootToken, root, StringComparison.Ordinal));
        }

        return this with
        {
            Root = root,
            LaunchSettingsJson = json,
            ProfileName = ProfileName?.Replace(RootToken, root, StringComparison.Ordinal),
            Environment = Environment.ToDictionary(pair => pair.Key, pair => pair.Value.Replace(RootToken, root, StringComparison.Ordinal)),
            Properties = Properties.ToDictionary(pair => pair.Key, pair => pair.Value.Replace(RootToken, root, StringComparison.Ordinal)),
            ArgumentOverride = ArgumentOverride?.Select(argument => argument.Replace(RootToken, root, StringComparison.Ordinal)).ToList(),
            MsBuildVariables = [.. MsBuildVariables.Select(variable => new MsBuildVariable(variable.Name, variable.PriorValue?.Replace(RootToken, root, StringComparison.Ordinal)))],
            ExpectedRequest = ExpectedRequest is null ? null : ReplaceRoot(ExpectedRequest, root),
        };
    }

    /// <summary>Builds the inputs the resolver test passes; the case must be materialized.</summary>
    /// <returns>The inputs.</returns>
    public RunInputs ToInputs() =>
        new()
        {
            ProjectFilePath = ProjectFilePath,
            OutputType = OutputType,
            Configuration = Configuration,
            LaunchSettings = LaunchSettingsReader.Read(Path.Combine(RequireRoot(), "App")),
            ProfileName = ProfileName,
            NoLaunchProfile = NoLaunchProfile,
            ArgumentOverride = ArgumentOverride,
            ProjectProperties = Properties,
            Environment = Environment,
            MsBuildVariables = MsBuildVariables,
        };

    /// <summary>Creates the planner a front end test uses for this case.</summary>
    /// <param name="projects">The project system the planner evaluates with.</param>
    /// <returns>A planner over the case's platform, environment and MSBuild variables.</returns>
    public RunPlanner CreatePlanner(IRunProjectSystem projects) =>
        new(projects, Platform, () => Environment, () => MsBuildVariables);

    private static ProcessStartRequest ReplaceRoot(ProcessStartRequest request, string root) =>
        new(
            AsPath(request.FileName, root),
            [.. request.Arguments.Select(argument => AsPath(argument, root))],
            AsPath(request.WorkingDirectory, root),
            request.EnvironmentVariables?.ToDictionary(pair => pair.Key, pair => pair.Value.Replace(RootToken, root, StringComparison.Ordinal)))
        {
            EnvironmentRemovals = [.. request.EnvironmentRemovals.Select(name => name.Replace(RootToken, root, StringComparison.Ordinal))],
        };

    private static string AsPath(string value, string root) =>
        value.StartsWith(RootToken, StringComparison.Ordinal)
            ? root + value[RootToken.Length..].Replace('/', Path.DirectorySeparatorChar).Replace(RootToken, root, StringComparison.Ordinal)
            : value.Replace(RootToken, root, StringComparison.Ordinal);

    private string RequireRoot() => Root ?? throw new InvalidOperationException($"The case '{Name}' is not materialized.");
}
