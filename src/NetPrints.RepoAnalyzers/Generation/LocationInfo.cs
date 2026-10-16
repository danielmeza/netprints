using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace NetPrints.RepoAnalyzers.Generation;

/// <summary>A source location that an incremental pipeline can hold: equatable, with no syntax tree or compilation behind it.</summary>
/// <param name="FilePath">The path of the syntax tree.</param>
/// <param name="Span">The text span.</param>
/// <param name="Lines">The line span.</param>
internal sealed record LocationInfo(string FilePath, TextSpan Span, LinePositionSpan Lines)
{
    /// <summary>Gets the location used when a symbol has no source.</summary>
    public static LocationInfo None { get; } = new(string.Empty, default, default);

    /// <summary>Captures a location.</summary>
    /// <param name="location">The location, or null.</param>
    /// <returns>The captured location, or <see cref="None"/> when it is not in source.</returns>
    public static LocationInfo From(Location? location) =>
        location is { SourceTree: { } tree }
            ? new LocationInfo(tree.FilePath, location.SourceSpan, location.GetLineSpan().Span)
            : None;

    /// <summary>Gets the location to report a diagnostic at.</summary>
    /// <returns>A source location, or <see cref="Location.None"/>.</returns>
    public Location ToLocation() => FilePath.Length == 0 ? Location.None : Location.Create(FilePath, Span, Lines);
}
