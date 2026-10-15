namespace NetPrints.Editor.Contributions.BuiltIn;

/// <summary>The built-in tooltip providers.</summary>
public static class TooltipContributions
{
    /// <summary>Registers the providers.</summary>
    /// <param name="registry">The registry to add to.</param>
    public static void Register(IContributionRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.AddTooltipProvider(new ConnectionTooltipProvider());
    }
}
