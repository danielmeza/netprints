using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;
using NetPrints.Graph;

namespace NetPrints.Extensibility.Editor;

/// <summary>The subject of a <see cref="TooltipTargetKind.Connection"/> tooltip: the model pins at the two ends of the connection.</summary>
/// <param name="Source">The output-side pin.</param>
/// <param name="Target">The input-side pin.</param>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record TooltipConnection(NodePin Source, NodePin Target);
