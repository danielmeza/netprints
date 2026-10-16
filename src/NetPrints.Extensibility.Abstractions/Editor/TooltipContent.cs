using System.Diagnostics.CodeAnalysis;
using NetPrints.Core;

namespace NetPrints.Extensibility.Editor;

/// <summary>The text of a tooltip.</summary>
/// <param name="Title">The heading.</param>
/// <param name="Lines">Detail lines under the heading.</param>
/// <param name="Documentation">Documentation text, or null.</param>
[Experimental(ExperimentalApiIds.EditorContributions, UrlFormat = ExperimentalApiIds.UrlFormat)]
public sealed record TooltipContent(string Title, IReadOnlyList<string> Lines, string? Documentation = null);
