namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in commands of the Go menu; they arrive with the navigation tasks (T039, T074 to T077).</summary>
public static class GoContributions
{
    /// <summary>Registers the Go menu's commands.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry) => ArgumentNullException.ThrowIfNull(registry);
}
