namespace NetPrints.Editor.Contributions;

/// <summary>Asks tooltip providers in <see cref="ITooltipProvider.Order"/> and takes the first content.</summary>
public static class TooltipResolver
{
    /// <summary>Resolves the tooltip of a target.</summary>
    /// <param name="providers">The registered providers, in any order.</param>
    /// <param name="target">What the pointer is over.</param>
    /// <returns>The first non-null content, or null when no provider has any.</returns>
    public static TooltipContent? Resolve(IEnumerable<ITooltipProvider> providers, TooltipTarget target)
    {
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(target);
        return providers.OrderBy(provider => provider.Order).Select(provider => provider.TryProvide(target)).FirstOrDefault(content => content is not null);
    }
}
