using NetPrints.Core;
using NetPrints.Projects;
using NetPrints.Workspace.Run;

namespace NetPrints.Run.Testing;

/// <summary>An <see cref="IRunProjectSystem"/> that answers from a fixed property table and records what it was asked.</summary>
public sealed class FakeRunProjectSystem : IRunProjectSystem
{
    private readonly Dictionary<string, string> _properties;

    /// <summary>Creates the fake.</summary>
    /// <param name="properties">The properties the fake project defines; names compare case-insensitively.</param>
    public FakeRunProjectSystem(IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        _properties = new Dictionary<string, string>(properties, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Gets the evaluations asked for, in order: the configuration and the names.</summary>
    public List<(string Configuration, IReadOnlyList<string> Names)> Evaluations { get; } = [];

    /// <summary>Gets the builds asked for, in order: the project file and the configuration.</summary>
    public List<(string ProjectFilePath, string Configuration)> Builds { get; } = [];

    /// <summary>Gets or sets what <see cref="BuildAsync"/> returns.</summary>
    public BuildResult BuildResult { get; set; } = new(true, [], null, string.Empty);

    /// <summary>Gets or sets an exception <see cref="BuildAsync"/> throws after recording the build.</summary>
    public Exception? ThrowOnBuild { get; set; }

    /// <summary>Creates the project system of a case: its properties plus the <c>OutputType</c> its output type maps to.</summary>
    /// <param name="runCase">The case.</param>
    /// <returns>The fake.</returns>
    public static FakeRunProjectSystem For(RunProfileCase runCase)
    {
        ArgumentNullException.ThrowIfNull(runCase);
        var all = new Dictionary<string, string>(runCase.Properties, StringComparer.OrdinalIgnoreCase)
        {
            ["OutputType"] = runCase.OutputType == BinaryType.Executable ? "Exe" : "Library",
        };
        return new FakeRunProjectSystem(all);
    }

    /// <inheritdoc/>
    public Task<BuildResult> BuildAsync(string projectFilePath, string configuration, CancellationToken cancellationToken)
    {
        Builds.Add((projectFilePath, configuration));
        return ThrowOnBuild is null ? Task.FromResult(BuildResult) : throw ThrowOnBuild;
    }

    /// <inheritdoc/>
    public Task<IReadOnlyDictionary<string, string>> EvaluatePropertiesAsync(string projectFilePath, string configuration, IReadOnlyList<string> names, CancellationToken cancellationToken)
    {
        Evaluations.Add((configuration, names));
        var answer = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string name in names)
        {
            if (_properties.TryGetValue(name, out string? value))
            {
                answer[name] = value;
            }
        }

        return Task.FromResult<IReadOnlyDictionary<string, string>>(answer);
    }
}
