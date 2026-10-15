namespace NetPrints.Editor.Contributions;

/// <summary>Supplies tooltip content for pins and connections.</summary>
public interface ITooltipProvider
{
    /// <summary>Gets the order in which providers are asked; the first non-null content wins.</summary>
    int Order { get; }

    /// <summary>Provides the tooltip for <paramref name="target"/>.</summary>
    /// <param name="target">What the pointer is over.</param>
    /// <returns>The content, or null when this provider has nothing to say.</returns>
    TooltipContent? TryProvide(TooltipTarget target);
}
