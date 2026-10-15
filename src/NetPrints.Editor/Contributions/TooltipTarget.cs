namespace NetPrints.Editor.Contributions;

/// <summary>What a tooltip is asked about.</summary>
/// <param name="Kind">The kind of item.</param>
/// <param name="Subject">The model or view model of the item.</param>
public sealed record TooltipTarget(TooltipTargetKind Kind, object Subject);

/// <summary>The kind of item under the pointer.</summary>
public enum TooltipTargetKind
{
    /// <summary>A node pin.</summary>
    Pin,

    /// <summary>A connection between pins.</summary>
    Connection,
}
