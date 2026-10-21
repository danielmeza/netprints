using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>What a tooltip is asked about.</summary>
/// <param name="Kind">The kind of item.</param>
/// <param name="Subject">The model of the item: a <see cref="NetPrints.Graph.NodePin"/> for <see cref="TooltipTargetKind.Pin"/>, a <see cref="TooltipConnection"/> for <see cref="TooltipTargetKind.Connection"/>; never an editor view model.</param>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record TooltipTarget(TooltipTargetKind Kind, object Subject);

/// <summary>The kind of item under the pointer.</summary>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public enum TooltipTargetKind
{
    /// <summary>A node pin.</summary>
    Pin,

    /// <summary>A connection between pins.</summary>
    Connection,
}
