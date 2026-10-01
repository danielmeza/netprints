namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in commands of the Help menu; they arrive with T055 and T066.</summary>
public static class HelpContributions
{
    /// <summary>Registers the Help menu's commands.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry) => ArgumentNullException.ThrowIfNull(registry);
}
